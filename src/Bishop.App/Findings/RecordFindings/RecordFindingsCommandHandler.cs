using System.Globalization;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CoreEntities = Bishop.Core;

namespace Bishop.App.Findings.RecordFindings;

internal sealed class RecordFindingsCommandHandler : IRequestHandler<RecordFindingsCommand, RecordFindingsResult>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecordFindingsCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task<RecordFindingsResult> Handle(RecordFindingsCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SkillName))
            throw new InvalidOperationException("Skill name must be provided.");
        if (Path.GetFileName(request.SkillName) != request.SkillName)
            throw new InvalidOperationException("Skill name must not contain path separators or traversal sequences.");
        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
            throw new InvalidOperationException("Workspace path must be provided.");
        if (string.IsNullOrWhiteSpace(request.GitSha))
            throw new InvalidOperationException("Git SHA must be provided.");

        var document = FindingsValidator.Parse(request.FindingsJson);
        var recordedAt = _timeProvider.GetUtcNow();
        var runProjectName = CoreEntities.PerProjectSkills.IsPerProject(request.SkillName)
            ? document.ProjectName
            : null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await ImportLegacyJsonIfPresentAsync(db, request, recordedAt, cancellationToken);

        var run = await db.WorkspaceSkillRuns
            .Include(r => r.Findings)
            .FirstOrDefaultAsync(
                r => r.WorkspaceId == request.WorkspaceId
                  && r.SkillName == request.SkillName
                  && r.ProjectName == runProjectName,
                cancellationToken);

        if (run is null)
        {
            run = new CoreEntities.WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                SkillName = request.SkillName,
                ProjectName = runProjectName,
                GitSha = request.GitSha,
                RecordedAt = recordedAt,
                FindingsCount = document.Findings.Count,
            };
            db.WorkspaceSkillRuns.Add(run);
        }
        else
        {
            run.GitSha = request.GitSha;
            run.RecordedAt = recordedAt;
            run.FindingsCount = document.Findings.Count;
        }

        var existingByHash = run.Findings.ToDictionary(f => f.IdentityHash, StringComparer.Ordinal);
        var existingByFileTitle = BuildFileTitleLookup(run.Findings);
        var incomingHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var f in document.Findings)
        {
            var hash = FindingIdentity.Compute(
                request.SkillName, document.ProjectName, f.File, f.Title);
            incomingHashes.Add(hash);

            var (outcomeStatus, outcomeCardNumber) = ParseOutcome(f.Outcome);

            // Migration window: a row may exist under a stale IdentityHash (computed from
            // the pre-#959 algorithm that included Rule/Symbol). Look it up by (File, Title)
            // when the hash doesn't match, and merge any duplicate rows into a single winner.
            var matches = CollectMatches(existingByHash, existingByFileTitle, hash, f.File, f.Title);
            if (matches.Count > 0)
            {
                var existing = MergeDuplicates(db, matches);
                existing.IdentityHash = hash;
                existing.LastSeenAt = recordedAt;
                existing.Severity = f.Severity;
                existing.Title = f.Title;
                existing.Body = f.Body;
                existing.File = f.File;
                existing.Symbol = f.Symbol;
                existing.Rule = f.Rule;
                if (existing.Status == "resolved")
                    existing.Status = "pending";

                // Apply skill-emitted carded:#N link only if the user hasn't already
                // dismissed or carded this finding manually.
                if (outcomeStatus == "carded"
                    && existing.LinkedCardId is null
                    && existing.Status is "pending" or "parked")
                {
                    existing.Status = "carded";
                    existing.LinkedCardId = outcomeCardNumber;
                }
            }
            else
            {
                db.Findings.Add(new CoreEntities.Finding
                {
                    Id = Guid.NewGuid(),
                    WorkspaceSkillRunId = run.Id,
                    IdentityHash = hash,
                    Status = outcomeStatus,
                    ProjectName = document.ProjectName,
                    File = f.File,
                    Symbol = f.Symbol,
                    Rule = f.Rule,
                    Severity = f.Severity,
                    Title = f.Title,
                    Body = f.Body,
                    FirstSeenAt = recordedAt,
                    LastSeenAt = recordedAt,
                    LinkedCardId = outcomeCardNumber,
                });
            }
        }

        foreach (var existing in run.Findings)
        {
            // Skip rows we already deleted via the migration-window merge above.
            if (db.Entry(existing).State == EntityState.Deleted)
                continue;
            if (!incomingHashes.Contains(existing.IdentityHash))
                existing.Status = "resolved";
        }

        await db.SaveChangesAsync(cancellationToken);

        return new RecordFindingsResult(document.Findings.Count);
    }

    // (File, Title) → all existing rows in this run that share that key.
    // Built case-sensitively to match how the new IdentityHash hashes its inputs.
    private static Dictionary<(string File, string Title), List<CoreEntities.Finding>> BuildFileTitleLookup(
        IEnumerable<CoreEntities.Finding> findings)
    {
        var map = new Dictionary<(string, string), List<CoreEntities.Finding>>();
        foreach (var f in findings)
        {
            if (string.IsNullOrEmpty(f.File))
                continue;
            var key = (f.File, f.Title);
            if (!map.TryGetValue(key, out var bucket))
            {
                bucket = new List<CoreEntities.Finding>();
                map[key] = bucket;
            }
            bucket.Add(f);
        }
        return map;
    }

    private static List<CoreEntities.Finding> CollectMatches(
        Dictionary<string, CoreEntities.Finding> byHash,
        Dictionary<(string File, string Title), List<CoreEntities.Finding>> byFileTitle,
        string hash,
        string? file,
        string title)
    {
        var matches = new List<CoreEntities.Finding>();
        if (byHash.TryGetValue(hash, out var hashMatch))
            matches.Add(hashMatch);

        if (!string.IsNullOrEmpty(file)
            && byFileTitle.TryGetValue((file, title), out var fileTitleMatches))
        {
            foreach (var m in fileTitleMatches)
            {
                if (!matches.Contains(m))
                    matches.Add(m);
            }
        }
        return matches;
    }

    // Status priority: carded > dismissed > parked > pending > resolved.
    // Returns the winner and deletes the rest, carrying LinkedCardId/RebuttalText forward.
    private static CoreEntities.Finding MergeDuplicates(
        BishopDbContext db,
        List<CoreEntities.Finding> matches)
    {
        if (matches.Count == 1)
            return matches[0];

        matches.Sort((a, b) => StatusPriority(b.Status).CompareTo(StatusPriority(a.Status)));
        var winner = matches[0];
        for (var i = 1; i < matches.Count; i++)
        {
            var loser = matches[i];
            winner.LinkedCardId ??= loser.LinkedCardId;
            winner.RebuttalText ??= loser.RebuttalText;
            db.Findings.Remove(loser);
        }
        return winner;
    }

    private static int StatusPriority(string status) => status switch
    {
        "carded" => 4,
        "dismissed" => 3,
        "parked" => 2,
        "pending" => 1,
        _ => 0, // resolved or anything unrecognised
    };

    private static (string Status, int? CardNumber) ParseOutcome(string outcome)
    {
        if (outcome == "dismissed") return ("dismissed", null);
        if (outcome.StartsWith("carded:#", StringComparison.Ordinal)
            && int.TryParse(outcome.AsSpan(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && n > 0)
            return ("carded", n);
        // "parked" and any other validator-accepted value land on pending.
        return ("pending", null);
    }

    private static async Task ImportLegacyJsonIfPresentAsync(
        BishopDbContext db,
        RecordFindingsCommand request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        var anyRun = await db.WorkspaceSkillRuns
            .AnyAsync(r => r.WorkspaceId == request.WorkspaceId && r.SkillName == request.SkillName, cancellationToken);
        if (anyRun)
            return;

        var legacyJsonPath = Path.Combine(request.WorkspacePath, ".bishop", "findings", $"{request.SkillName}.json");
        if (!File.Exists(legacyJsonPath))
            return;

        FindingsDocument legacyDoc;
        try
        {
            var legacyJson = await File.ReadAllTextAsync(legacyJsonPath, cancellationToken);
            legacyDoc = FindingsValidator.Parse(legacyJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to import legacy findings file '{legacyJsonPath}': {ex.Message}. Delete the file or fix it to continue.",
                ex);
        }

        var legacyRun = new CoreEntities.WorkspaceSkillRun
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            SkillName = request.SkillName,
            ProjectName = CoreEntities.PerProjectSkills.IsPerProject(request.SkillName) ? legacyDoc.ProjectName : null,
            GitSha = request.GitSha,
            RecordedAt = recordedAt,
            FindingsCount = legacyDoc.Findings.Count,
        };
        db.WorkspaceSkillRuns.Add(legacyRun);

        foreach (var f in legacyDoc.Findings)
        {
            var (status, cardNumber) = ParseOutcome(f.Outcome);
            db.Findings.Add(new CoreEntities.Finding
            {
                Id = Guid.NewGuid(),
                WorkspaceSkillRunId = legacyRun.Id,
                IdentityHash = FindingIdentity.Compute(
                    request.SkillName, legacyDoc.ProjectName, f.File, f.Title),
                Status = status,
                ProjectName = legacyDoc.ProjectName,
                File = f.File,
                Symbol = f.Symbol,
                Rule = f.Rule,
                Severity = f.Severity,
                Title = f.Title,
                Body = f.Body,
                FirstSeenAt = recordedAt,
                LastSeenAt = recordedAt,
                LinkedCardId = cardNumber,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        File.Delete(legacyJsonPath);
    }

}

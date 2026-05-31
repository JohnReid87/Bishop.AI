using System.Globalization;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using CoreEntities = Bishop.Core;

namespace Bishop.App.Findings.RecordFindings;

public sealed class RecordFindingsCommandHandler : IRequestHandler<RecordFindingsCommand, RecordFindingsResult>
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

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await ImportLegacyJsonIfPresentAsync(db, request, recordedAt, cancellationToken);

        var run = await db.WorkspaceSkillRuns
            .Include(r => r.Findings)
            .FirstOrDefaultAsync(
                r => r.WorkspaceId == request.WorkspaceId
                  && r.SkillName == request.SkillName
                  && r.ProjectName == document.ProjectName,
                cancellationToken);

        if (run is null)
        {
            run = new CoreEntities.WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                SkillName = request.SkillName,
                ProjectName = document.ProjectName,
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
        var incomingHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var f in document.Findings)
        {
            var hash = FindingIdentity.Compute(
                request.SkillName, document.ProjectName, f.File, f.Rule, f.Symbol, f.Title);
            incomingHashes.Add(hash);

            var (outcomeStatus, outcomeCardNumber) = ParseOutcome(f.Outcome);

            if (existingByHash.TryGetValue(hash, out var existing))
            {
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
            if (!incomingHashes.Contains(existing.IdentityHash))
                existing.Status = "resolved";
        }

        await db.SaveChangesAsync(cancellationToken);

        return new RecordFindingsResult(document.Findings.Count);
    }

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
            ProjectName = legacyDoc.ProjectName,
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
                    request.SkillName, legacyDoc.ProjectName, f.File, f.Rule, f.Symbol, f.Title),
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

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

        var run = await db.WorkspaceSkillRuns
            .Include(r => r.Findings)
            .FirstOrDefaultAsync(
                r => r.WorkspaceId == request.WorkspaceId
                  && r.SkillName == request.SkillName
                  && r.ProjectName == runProjectName
                  && r.BatchId == request.BatchId,
                cancellationToken);

        if (run is null)
        {
            run = new CoreEntities.WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                SkillName = request.SkillName,
                ProjectName = runProjectName,
                BatchId = request.BatchId,
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
        var existingByFileTitle = FindingMatcher.BuildFileTitleLookup(run.Findings);
        var incomingHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var f in document.Findings)
        {
            var hash = FindingIdentity.Compute(
                request.SkillName, document.ProjectName, f.File, f.Title);
            incomingHashes.Add(hash);

            var (outcomeStatus, outcomeCardNumber) = FindingOutcomeParser.Parse(f.Outcome);

            // Migration window: a row may exist under a stale IdentityHash (computed from
            // the pre-#959 algorithm that included Rule/Symbol). Look it up by (File, Title)
            // when the hash doesn't match, and merge any duplicate rows into a single winner.
            var matches = FindingMatcher.CollectMatches(existingByHash, existingByFileTitle, hash, f.File, f.Title);
            if (matches.Count > 0)
            {
                var existing = FindingMatcher.MergeDuplicates(db, matches);
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
}

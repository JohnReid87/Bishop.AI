using System.Globalization;
using Bishop.Data;
using Microsoft.EntityFrameworkCore;
using CoreEntities = Bishop.Core;

namespace Bishop.App.Findings.RecordFindings;

internal static class LegacyFindingsImporter
{
    internal static async Task ImportIfPresentAsync(
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

    private static (string Status, int? CardNumber) ParseOutcome(string outcome)
    {
        if (outcome == "dismissed") return ("dismissed", null);
        if (outcome.StartsWith("carded:#", StringComparison.Ordinal)
            && int.TryParse(outcome.AsSpan(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && n > 0)
            return ("carded", n);
        return ("pending", null);
    }
}

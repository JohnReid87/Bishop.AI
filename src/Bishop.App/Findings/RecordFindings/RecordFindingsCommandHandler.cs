using System.Text.Json;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Findings.RecordFindings;

public sealed class RecordFindingsCommandHandler : IRequestHandler<RecordFindingsCommand, RecordFindingsResult>
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RecordFindingsCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

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

        var findingsDir = Path.Combine(request.WorkspacePath, ".bishop", "findings");
        Directory.CreateDirectory(findingsDir);

        var jsonPath = Path.Combine(findingsDir, $"{request.SkillName}.json");
        var htmlPath = Path.Combine(findingsDir, $"{request.SkillName}.html");

        var recordedAt = DateTimeOffset.UtcNow;
        var canonicalJson = JsonSerializer.Serialize(document, CanonicalJsonOptions);
        var html = FindingsHtmlRenderer.Render(request.SkillName, document, recordedAt, request.GitSha);

        await File.WriteAllTextAsync(jsonPath, canonicalJson, cancellationToken);
        await File.WriteAllTextAsync(htmlPath, html, cancellationToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.WorkspaceSkillRuns
            .FirstOrDefaultAsync(r => r.WorkspaceId == request.WorkspaceId && r.SkillName == request.SkillName, cancellationToken);

        var findingsCount = document.Findings.Count;

        if (run is null)
        {
            db.WorkspaceSkillRuns.Add(new WorkspaceSkillRun
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                SkillName = request.SkillName,
                GitSha = request.GitSha,
                RecordedAt = recordedAt,
                FindingsCount = findingsCount,
            });
        }
        else
        {
            run.GitSha = request.GitSha;
            run.RecordedAt = recordedAt;
            run.FindingsCount = findingsCount;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new RecordFindingsResult(jsonPath, htmlPath, document.Findings.Count);
    }
}

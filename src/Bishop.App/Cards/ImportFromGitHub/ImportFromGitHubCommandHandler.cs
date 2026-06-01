using System.Data;
using System.Text.Json;
using Bishop.App.Services.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ImportFromGitHub;

public sealed class ImportFromGitHubCommandHandler : IRequestHandler<ImportFromGitHubCommand, ImportFromGitHubResult>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGhCli _ghCli;

    public ImportFromGitHubCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, IGhCli ghCli)
    {
        _dbFactory = dbFactory;
        _ghCli = ghCli;
    }

    public async Task<ImportFromGitHubResult> Handle(ImportFromGitHubCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var workspace = await db.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

        var repo = workspace.GitHubRepo
            ?? throw new InvalidOperationException(
                $"Workspace '{workspace.Name}' has no GitHub repo configured. Run: bishop workspace set-github <owner/repo>");

        var backlogLaneName = SystemLaneNames.Backlog;

        var tagNameSet = BrandTagPalette.DefaultColours.Keys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ghArgs = new List<string>
        {
            "issue", "list",
            "--repo", repo,
            "--state", "open",
            "--limit", request.Limit.ToString(),
            "--json", "number,title,body,labels",
        };
        if (request.LabelFilter is not null)
        {
            ghArgs.Add("--label");
            ghArgs.Add(request.LabelFilter);
        }

        var json = await _ghCli.RunCaptureAsync([.. ghArgs], cancellationToken);
        var issues = JsonSerializer.Deserialize<List<GhIssue>>(json, GhJsonOptions) ?? [];
        issues.Sort((a, b) => a.Number.CompareTo(b.Number));

        var existingIssueNumbers = await db.Cards
            .Where(c => c.WorkspaceId == request.WorkspaceId && c.GitHubIssueNumber.HasValue)
            .Select(c => c.GitHubIssueNumber!.Value)
            .ToHashSetAsync(cancellationToken);

        if (request.DryRun)
        {
            var dryImported = new List<Card>();
            var drySkipped = new List<int>();

            foreach (var issue in issues)
            {
                if (existingIssueNumbers.Contains(issue.Number))
                    drySkipped.Add(issue.Number);
                else
                    dryImported.Add(new Card { Title = issue.Title, GitHubIssueNumber = issue.Number });
            }

            return new ImportFromGitHubResult(dryImported, drySkipped, []);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var maxPosition = await db.Cards
            .Where(c => c.WorkspaceId == request.WorkspaceId && c.LaneName == backlogLaneName)
            .MaxAsync(c => (int?)c.Position, cancellationToken) ?? 0;

        var imported = new List<Card>();
        var skipped = new List<int>();

        foreach (var issue in issues)
        {
            if (existingIssueNumbers.Contains(issue.Number))
            {
                skipped.Add(issue.Number);
                continue;
            }

            var issueBody = issue.Body ?? string.Empty;
            var footer = $"---\n*Imported from GitHub issue #{issue.Number} ({repo}).*";
            var description = string.IsNullOrWhiteSpace(issueBody)
                ? footer
                : $"{issueBody}\n\n{footer}";

            var firstTagName = issue.Labels
                .Select(l => l.Name)
                .FirstOrDefault(n => tagNameSet.Contains(n));

            var card = new Card
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                LaneName = backlogLaneName,
                TagName = firstTagName,
                Title = issue.Title,
                Description = description,
                Number = workspace.NextCardNumber++,
                Position = ++maxPosition,
                GitHubIssueNumber = issue.Number,
            };
            db.Cards.Add(card);
            imported.Add(card);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new ImportFromGitHubResult(imported, skipped, []);
    }

    private static readonly JsonSerializerOptions GhJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class GhIssue
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public List<GhLabel> Labels { get; set; } = [];
    }

    private sealed class GhLabel
    {
        public string Name { get; set; } = string.Empty;
    }
}

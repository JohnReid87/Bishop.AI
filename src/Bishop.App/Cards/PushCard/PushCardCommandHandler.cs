using Bishop.App.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.PushCard;

public sealed class PushCardCommandHandler : IRequestHandler<PushCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGhCli _ghCli;

    public PushCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, IGhCli ghCli)
    {
        _dbFactory = dbFactory;
        _ghCli = ghCli;
    }

    public async Task<Card> Handle(PushCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        var repo = card.Workspace.GitHubRepo
            ?? throw new InvalidOperationException($"Workspace '{card.Workspace.Name}' has no GitHub repo configured. Run: bishop workspace set-github <owner/repo>");

        if (card.GitHubIssueNumber.HasValue)
            throw new InvalidOperationException($"Card #{card.Number} is already linked to GitHub issue #{card.GitHubIssueNumber}.");

        Tag? tag = null;
        if (card.TagName is { } tagName)
        {
            tag = await db.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.WorkspaceId == card.WorkspaceId && t.Name == tagName,
                    cancellationToken);
        }

        // Ensure the label exists on GitHub (ignore failures — label may already exist or permissions may be limited)
        if (tag is not null)
        {
            var color = tag.Colour.TrimStart('#');
            try
            {
                await _ghCli.RunAsync(["label", "create", tag.Name, "--color", color, "--repo", repo, "--force"], cancellationToken);
            }
            catch { /* label creation is best-effort */ }
        }

        // Build issue body with Bishop footer
        var body = string.IsNullOrWhiteSpace(card.Description)
            ? $"---\nBishop card {card.Number}"
            : $"{card.Description}\n\n---\nBishop card {card.Number}";

        // Build gh issue create args
        var createArgs = new List<string> { "issue", "create", "--repo", repo, "--title", card.Title, "--body", body };
        if (card.TagName is { } labelName)
        {
            createArgs.Add("--label");
            createArgs.Add(labelName);
        }

        var issueUrl = await _ghCli.RunCaptureAsync([.. createArgs], cancellationToken);

        // Parse issue number from URL (https://github.com/owner/repo/issues/123)
        if (!int.TryParse(issueUrl.Split('/').Last(), out var issueNumber))
            throw new InvalidOperationException($"Could not parse issue number from gh output: {issueUrl}");

        // Mirror closed state
        if (card.IsClosed)
            await _ghCli.RunAsync(["issue", "close", issueNumber.ToString(), "--repo", repo], cancellationToken);

        card.GitHubIssueNumber = issueNumber;
        card.GitHubPushedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return card;
    }
}

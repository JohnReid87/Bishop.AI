using Bishop.App.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.PushCard;

public sealed class PushCardCommandHandler : IRequestHandler<PushCardCommand, Card>
{
    private readonly BishopDbContext _db;
    private readonly IGhCli _ghCli;

    public PushCardCommandHandler(BishopDbContext db, IGhCli ghCli)
    {
        _db = db;
        _ghCli = ghCli;
    }

    public async Task<Card> Handle(PushCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards
            .Include(c => c.Lane)
                .ThenInclude(l => l.Workspace)
            .Include(c => c.CardTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        var repo = card.Lane.Workspace.GitHubRepo
            ?? throw new InvalidOperationException($"Workspace '{card.Lane.Workspace.Name}' has no GitHub repo configured. Run: bishop workspace set-github <owner/repo>");

        if (card.GitHubIssueNumber.HasValue)
            throw new InvalidOperationException($"Card #{card.Number} is already linked to GitHub issue #{card.GitHubIssueNumber}.");

        var tags = card.CardTags.Select(ct => ct.Tag).OrderBy(t => t.Name).ToList();

        // Ensure labels exist on GitHub (ignore failures — label may already exist or permissions may be limited)
        foreach (var tag in tags)
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
            ? $"---\nBishop card #{card.Number}"
            : $"{card.Description}\n\n---\nBishop card #{card.Number}";

        // Build gh issue create args
        var createArgs = new List<string> { "issue", "create", "--repo", repo, "--title", card.Title, "--body", body };
        foreach (var tag in tags)
        {
            createArgs.Add("--label");
            createArgs.Add(tag.Name);
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
        await _db.SaveChangesAsync(cancellationToken);
        return card;
    }
}

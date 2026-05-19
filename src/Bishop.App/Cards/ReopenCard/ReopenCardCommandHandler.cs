using Bishop.App.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ReopenCard;

public sealed class ReopenCardCommandHandler : IRequestHandler<ReopenCardCommand, Card>
{
    private readonly BishopDbContext _db;

    public ReopenCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Card> Handle(ReopenCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards
            .Include(c => c.Lane)
                .ThenInclude(l => l.Workspace)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = false;
        await _db.SaveChangesAsync(cancellationToken);

        if (card.GitHubIssueNumber.HasValue && card.Lane.Workspace.GitHubRepo is { } repo)
            await GhCli.RunAsync(["issue", "reopen", card.GitHubIssueNumber.ToString()!, "--repo", repo], cancellationToken);

        return card;
    }
}

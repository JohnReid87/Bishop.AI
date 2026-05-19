using Bishop.App.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.CloseCard;

public sealed class CloseCardCommandHandler : IRequestHandler<CloseCardCommand, Card>
{
    private readonly BishopDbContext _db;

    public CloseCardCommandHandler(BishopDbContext db) => _db = db;

    public async Task<Card> Handle(CloseCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards
            .Include(c => c.Lane)
                .ThenInclude(l => l.Workspace)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = true;
        await _db.SaveChangesAsync(cancellationToken);

        if (card.GitHubIssueNumber.HasValue && card.Lane.Workspace.GitHubRepo is { } repo)
            await GhCli.RunAsync(["issue", "close", card.GitHubIssueNumber.ToString()!, "--repo", repo], cancellationToken);

        return card;
    }
}

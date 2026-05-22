using Bishop.App.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.ReopenCard;

public sealed class ReopenCardCommandHandler : IRequestHandler<ReopenCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGhCli _ghCli;

    public ReopenCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, IGhCli ghCli)
    {
        _dbFactory = dbFactory;
        _ghCli = ghCli;
    }

    public async Task<Card> Handle(ReopenCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .Include(c => c.Lane)
                .ThenInclude(l => l.Workspace)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = false;
        await db.SaveChangesAsync(cancellationToken);

        if (card.GitHubIssueNumber.HasValue && card.Lane.Workspace.GitHubRepo is { } repo)
            await _ghCli.RunAsync(["issue", "reopen", card.GitHubIssueNumber.ToString()!, "--repo", repo], cancellationToken);

        return card;
    }
}

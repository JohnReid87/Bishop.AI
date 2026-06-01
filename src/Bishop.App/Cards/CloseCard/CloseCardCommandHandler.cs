using Bishop.App.Services.GitHub;
using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.CloseCard;

internal sealed class CloseCardCommandHandler : IRequestHandler<CloseCardCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly IGhCli _ghCli;

    public CloseCardCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, IGhCli ghCli)
    {
        _dbFactory = dbFactory;
        _ghCli = ghCli;
    }

    public async Task<Card> Handle(CloseCardCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.IsClosed = true;
        await db.SaveChangesAsync(cancellationToken);

        if (card.GitHubIssueNumber.HasValue && card.Workspace.GitHubRepo is { } repo)
            await _ghCli.RunAsync(["issue", "close", card.GitHubIssueNumber.ToString()!, "--repo", repo], cancellationToken);

        return card;
    }
}

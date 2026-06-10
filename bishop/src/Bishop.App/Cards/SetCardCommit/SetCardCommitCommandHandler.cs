using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.SetCardCommit;

internal sealed class SetCardCommitCommandHandler : IRequestHandler<SetCardCommitCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public SetCardCommitCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task<Card> Handle(SetCardCommitCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.CommitHash = request.Hash;
        card.BranchName = request.Branch;
        card.UpdatedAt = _timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}

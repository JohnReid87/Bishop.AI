using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.SetCardCommit;

public sealed class SetCardCommitCommandHandler : IRequestHandler<SetCardCommitCommand, Card>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public SetCardCommitCommandHandler(IDbContextFactory<BishopDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<Card> Handle(SetCardCommitCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.CommitHash = request.Hash;
        card.BranchName = request.Branch;
        card.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return card;
    }
}

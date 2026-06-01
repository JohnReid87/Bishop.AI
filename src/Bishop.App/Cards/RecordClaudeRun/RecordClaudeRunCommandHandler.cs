using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RecordClaudeRun;

internal sealed class RecordClaudeRunCommandHandler : IRequestHandler<RecordClaudeRunCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;

    public RecordClaudeRunCommandHandler(IDbContextFactory<BishopDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task Handle(RecordClaudeRunCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.TotalInputTokens += request.InputTokens;
        card.TotalOutputTokens += request.OutputTokens;
        card.TotalCacheCreationTokens += request.CacheCreationTokens;
        card.TotalCacheReadTokens += request.CacheReadTokens;
        card.TotalCostUsd += request.CostUsd;
        card.ClaudeRunCount += 1;

        await db.SaveChangesAsync(cancellationToken);
    }
}

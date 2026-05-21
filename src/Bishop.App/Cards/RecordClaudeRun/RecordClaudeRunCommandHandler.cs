using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RecordClaudeRun;

public sealed class RecordClaudeRunCommandHandler : IRequestHandler<RecordClaudeRunCommand>
{
    private readonly BishopDbContext _db;

    public RecordClaudeRunCommandHandler(BishopDbContext db) => _db = db;

    public async Task Handle(RecordClaudeRunCommand request, CancellationToken cancellationToken)
    {
        var card = await _db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.TotalCostUsd += request.CostUsd;
        card.TotalInputTokens += request.InputTokens;
        card.TotalOutputTokens += request.OutputTokens;
        card.ClaudeRunCount += 1;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

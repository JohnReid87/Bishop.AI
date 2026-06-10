using Bishop.Core;
using Bishop.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Bishop.App.Cards.RecordCardSuccess;

internal sealed class RecordCardSuccessCommandHandler : IRequestHandler<RecordCardSuccessCommand>
{
    private readonly IDbContextFactory<BishopDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecordCardSuccessCommandHandler(IDbContextFactory<BishopDbContext> dbFactory, TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task Handle(RecordCardSuccessCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.TotalInputTokens += request.InputTokens;
        card.TotalOutputTokens += request.OutputTokens;
        card.TotalCacheCreationTokens += request.CacheCreationTokens;
        card.TotalCacheReadTokens += request.CacheReadTokens;
        card.TotalCostUsd += request.CostUsd;
        card.ClaudeRunCount += 1;

        card.CommitHash = request.CommitHash;
        card.BranchName = request.BranchName;

        if (!string.IsNullOrEmpty(request.AppendDescription))
        {
            card.Description = string.IsNullOrEmpty(card.Description)
                ? request.AppendDescription
                : $"{card.Description}\n\n---\n\n{request.AppendDescription}";
        }

        var now = _timeProvider.GetUtcNow();
        card.LastAutoRunSucceededAt = now;
        card.UpdatedAt = now;

        var sourceLane = card.LaneName;
        if (sourceLane != SystemLaneNames.Done)
        {
            var sourceCards = await db.Cards
                .Where(c => c.WorkspaceId == card.WorkspaceId && c.LaneName == sourceLane && c.Id != card.Id)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);
            for (var i = 0; i < sourceCards.Count; i++)
                sourceCards[i].Position = i + 1;

            var doneCards = await db.Cards
                .Where(c => c.WorkspaceId == card.WorkspaceId && c.LaneName == SystemLaneNames.Done && c.Id != card.Id)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);
            doneCards.Insert(0, card);
            card.LaneName = SystemLaneNames.Done;
            for (var i = 0; i < doneCards.Count; i++)
                doneCards[i].Position = i + 1;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

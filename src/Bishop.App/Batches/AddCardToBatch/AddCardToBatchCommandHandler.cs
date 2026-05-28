using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Batches.AddCardToBatch;

public sealed class AddCardToBatchCommandHandler : IRequestHandler<AddCardToBatchCommand, Unit>
{
    private readonly IBatchRepository _batches;

    public AddCardToBatchCommandHandler(IBatchRepository batches) => _batches = batches;

    public async Task<Unit> Handle(AddCardToBatchCommand request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.BatchName, cancellationToken);

        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.BatchName}' found.");

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.BatchName}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        if (batch.Status != BatchStatus.Open)
            throw new InvalidOperationException(
                $"Batch '{batch.Name}' is {batch.Status} — only Open batches accept card changes.");

        await _batches.AssignCardAsync(batch.Id, request.CardId, cancellationToken);

        return Unit.Value;
    }
}

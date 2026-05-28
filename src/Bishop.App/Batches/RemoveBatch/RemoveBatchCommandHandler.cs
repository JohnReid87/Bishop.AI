using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Batches.RemoveBatch;

public sealed class RemoveBatchCommandHandler : IRequestHandler<RemoveBatchCommand>
{
    private readonly IBatchRepository _batches;

    public RemoveBatchCommandHandler(IBatchRepository batches) => _batches = batches;

    public async Task Handle(RemoveBatchCommand request, CancellationToken cancellationToken)
    {
        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];
        if (batch.Status != BatchStatus.Closed)
            throw new InvalidOperationException(
                $"Batch '{request.Name}' must be Closed to remove; current status is {batch.Status}.");

        await _batches.DeleteAsync(batch.Id, cancellationToken);
    }
}

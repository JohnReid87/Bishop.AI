using Bishop.Core;
using Bishop.Data;
using MediatR;

namespace Bishop.App.Batches.RenameBatch;

public sealed class RenameBatchCommandHandler : IRequestHandler<RenameBatchCommand, Batch>
{
    private readonly IBatchRepository _batches;

    public RenameBatchCommandHandler(IBatchRepository batches) => _batches = batches;

    public async Task<Batch> Handle(RenameBatchCommand request, CancellationToken cancellationToken)
    {
        var trimmed = request.NewName.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Batch name cannot be empty.");

        var matches = await _batches.GetByNameAsync(request.Name, cancellationToken);
        if (matches.Count == 0)
            throw new InvalidOperationException($"No batch named '{request.Name}' found.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Multiple batches named '{request.Name}' exist; use the branch name to disambiguate: "
                + string.Join(", ", matches.Select(b => b.BranchName)));

        var batch = matches[0];

        if (batch.Name == trimmed)
            return batch;

        var conflicts = await _batches.GetByNameAsync(trimmed, cancellationToken);
        if (conflicts.Any(b => b.Id != batch.Id && b.Status != BatchStatus.Closed))
            throw new InvalidOperationException($"An active batch named '{trimmed}' already exists.");

        return await _batches.RenameAsync(batch.Id, trimmed, cancellationToken);
    }
}

using MediatR;

namespace Bishop.App.Batches.GetBatch;

public sealed record GetBatchQuery(string Name) : IRequest<GetBatchResult>;

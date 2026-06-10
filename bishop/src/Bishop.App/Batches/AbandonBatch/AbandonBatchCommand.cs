using MediatR;

namespace Bishop.App.Batches.AbandonBatch;

public sealed record AbandonBatchCommand(string Name, string WorkspacePath) : IRequest<AbandonBatchResult>;

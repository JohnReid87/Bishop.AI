using MediatR;

namespace Bishop.App.Batches.FinishBatch;

public sealed record FinishBatchCommand(string Name, string WorkspacePath, string GitHubRepo) : IRequest<FinishBatchResult>;

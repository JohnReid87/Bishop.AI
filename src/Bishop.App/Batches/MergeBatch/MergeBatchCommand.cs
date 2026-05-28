using MediatR;

namespace Bishop.App.Batches.MergeBatch;

public sealed record MergeBatchCommand(string Name, string WorkspacePath) : IRequest<MergeBatchResult>;

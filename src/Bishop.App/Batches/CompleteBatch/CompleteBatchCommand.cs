using MediatR;

namespace Bishop.App.Batches.CompleteBatch;

public sealed record CompleteBatchCommand(string Name, string WorkspacePath) : IRequest;

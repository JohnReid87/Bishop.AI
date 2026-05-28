using MediatR;

namespace Bishop.App.Batches.CleanUpBatch;

public sealed record CleanUpBatchCommand(string Name, string WorkspacePath) : IRequest;

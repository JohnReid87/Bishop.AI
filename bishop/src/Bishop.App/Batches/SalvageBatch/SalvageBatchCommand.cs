using MediatR;

namespace Bishop.App.Batches.SalvageBatch;

public sealed record SalvageBatchCommand(string Name, string WorkspacePath, bool Confirm) : IRequest<SalvageBatchResult>;

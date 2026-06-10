using MediatR;

namespace Bishop.App.Batches.DeleteBatchBranch;

public sealed record DeleteBatchBranchCommand(string WorkspacePath, string BranchName) : IRequest<DeleteBatchBranchResult>;

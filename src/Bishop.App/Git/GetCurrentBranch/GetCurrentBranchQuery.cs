using MediatR;

namespace Bishop.App.Git.GetCurrentBranch;

public sealed record GetCurrentBranchQuery(string WorkspacePath) : IRequest<string>;

using MediatR;

namespace Bishop.App.Git.GetCardCommit;

public sealed record GetCardCommitQuery(int CardNumber, string WorkspacePath) : IRequest<GetCardCommitResult>;

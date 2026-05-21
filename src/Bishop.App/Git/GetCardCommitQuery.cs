using MediatR;

namespace Bishop.App.Git;

public sealed record GetCardCommitQuery(int CardNumber, string WorkspacePath) : IRequest<GetCardCommitResult>;

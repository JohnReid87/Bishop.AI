using MediatR;

namespace Bishop.App.Git;

public sealed record GetRecentCommitsQuery(string WorkspacePath) : IRequest<GetRecentCommitsResult>;

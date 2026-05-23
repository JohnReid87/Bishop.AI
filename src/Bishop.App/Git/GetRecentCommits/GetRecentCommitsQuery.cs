using MediatR;

namespace Bishop.App.Git.GetRecentCommits;

public sealed record GetRecentCommitsQuery(string WorkspacePath) : IRequest<GetRecentCommitsResult>;

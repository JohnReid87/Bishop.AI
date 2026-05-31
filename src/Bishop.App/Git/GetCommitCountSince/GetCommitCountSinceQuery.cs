using MediatR;

namespace Bishop.App.Git.GetCommitCountSince;

public sealed record GetCommitCountSinceQuery(string GitSha, string WorkspacePath) : IRequest<int?>;

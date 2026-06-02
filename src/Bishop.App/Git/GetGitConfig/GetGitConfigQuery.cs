using MediatR;

namespace Bishop.App.Git.GetGitConfig;

public sealed record GetGitConfigQuery(string WorkspacePath) : IRequest<GetGitConfigResult>;

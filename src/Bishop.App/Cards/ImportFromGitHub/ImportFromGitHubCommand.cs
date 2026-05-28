using MediatR;

namespace Bishop.App.Cards.ImportFromGitHub;

public sealed record ImportFromGitHubCommand(
    Guid WorkspaceId,
    string? LabelFilter,
    int Limit,
    bool DryRun) : IRequest<ImportFromGitHubResult>;

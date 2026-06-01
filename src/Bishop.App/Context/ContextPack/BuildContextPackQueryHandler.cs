using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTags;
using MediatR;

namespace Bishop.App.Context.ContextPack;

internal sealed class BuildContextPackQueryHandler : IRequestHandler<BuildContextPackQuery, ContextPack>
{
    internal const int ContextMdMaxBytes = 32 * 1024;
    private const string ContextMdFileName = "CONTEXT.md";

    private readonly IReadOnlyDictionary<string, IContextProvider> _providers;
    private readonly IGitCli _gitCli;
    private readonly ISender _mediator;

    public BuildContextPackQueryHandler(
        IEnumerable<IContextProvider> providers,
        IGitCli gitCli,
        ISender mediator)
    {
        _providers = providers.ToDictionary(p => p.SkillName, StringComparer.OrdinalIgnoreCase);
        _gitCli = gitCli;
        _mediator = mediator;
    }

    public async Task<ContextPack> Handle(BuildContextPackQuery request, CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(request.SkillName, out var provider))
        {
            var known = string.Join(", ", _providers.Keys.OrderBy(k => k, StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"No context provider registered for skill \"{request.SkillName}\". Known providers: {known}");
        }

        var workspace = request.Workspace;

        var workspaceBlock = await BuildWorkspaceBlockAsync(workspace, cancellationToken);
        var gitBlock = await BuildGitBlockAsync(workspace.Path, cancellationToken);
        var skillSpecific = await provider.BuildSkillSpecificAsync(request.Args, workspace, _mediator, cancellationToken);
        var conventions = StaticContextSections.Slice(provider.RequiredSections);

        return new ContextPack(workspaceBlock, gitBlock, skillSpecific, conventions);
    }

    private async Task<WorkspaceBlock> BuildWorkspaceBlockAsync(Bishop.Core.Workspace workspace, CancellationToken cancellationToken)
    {
        var lanes = await _mediator.Send(new ListLanesByWorkspaceQuery(workspace.Id), cancellationToken);
        var tags = await _mediator.Send(new ListTagsQuery(), cancellationToken);

        var (contextMd, truncated) = ReadContextMd(workspace.Path);

        return new WorkspaceBlock(
            workspace.Name,
            workspace.Path,
            workspace.GitHubRepo,
            lanes.OrderBy(l => l.Position).Select(l => l.Name).ToList(),
            tags.Select(t => t.Name).ToList(),
            contextMd,
            truncated);
    }

    private static (string? Content, bool Truncated) ReadContextMd(string workspacePath)
    {
        try
        {
            var path = Path.Combine(workspacePath, ContextMdFileName);
            if (!File.Exists(path)) return (null, false);

            var info = new FileInfo(path);
            if (info.Length > ContextMdMaxBytes)
                return (null, true);

            return (File.ReadAllText(path), false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, false);
        }
    }

    private async Task<GitBlock> BuildGitBlockAsync(string workspacePath, CancellationToken cancellationToken)
    {
        string? branch = null;
        try
        {
            branch = await _gitCli.GetCurrentBranchAsync(workspacePath, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Not a git repo or git unavailable — branch stays null.
        }

        var commitsResult = await _gitCli.GetRecentCommitsAsync(workspacePath, cancellationToken);
        var commits = commitsResult switch
        {
            GetRecentCommitsResult.Success success => success.Commits
                .Select(c => new CommitSummary(c.ShortHash, c.Subject, c.Timestamp))
                .ToList(),
            _ => new List<CommitSummary>()
        };

        return new GitBlock(branch, commits);
    }
}

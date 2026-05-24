using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Workspaces.SetGitHub;

internal sealed class SetGitHubCliCommand : Command
{
    public SetGitHubCliCommand(ISender mediator)
        : base("set-github", "Associate workspace with a GitHub repo")
    {
        var resolver = new WorkspaceResolver(mediator);
        var repoArg = new Argument<string>("repo", "GitHub repo in owner/repo format");

        AddArgument(repoArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string repo, string? workspace) =>
        {
            if (!repo.Contains('/') || repo.StartsWith('/') || repo.EndsWith('/'))
            {
                Console.Error.WriteLine($"Invalid repo '{repo}': expected owner/repo format (e.g. JohnReid87/MyProject).");
                Environment.ExitCode = 1;
                return;
            }
            var ws = await resolver.ResolveAsync(workspace);
            await mediator.Send(new SetWorkspaceGitHubRepoCommand(ws.Id, repo));
            Console.WriteLine($"Workspace '{ws.Name}' linked to GitHub repo '{repo}'");
        }, repoArg, CommonOptions.WorkspaceOption);
    }
}

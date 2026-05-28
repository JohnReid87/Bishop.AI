using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Workspaces.UnsetGitHub;

internal sealed class UnsetGitHubCliCommand : Command
{
    public UnsetGitHubCliCommand(ISender mediator)
        : base("unset-github", "Remove the GitHub repo association from a workspace")
    {
        var resolver = new WorkspaceResolver(mediator);

        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            await mediator.Send(new UnsetWorkspaceGitHubRepoCommand(ws.Id));
            Console.WriteLine($"Removed GitHub repo association from workspace '{ws.Name}'");
        }, CommonOptions.WorkspaceOption);
    }
}

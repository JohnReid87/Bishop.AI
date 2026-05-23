using Bishop.App.Workspaces.InitWorkspace;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Workspaces.Init;

internal sealed class InitWorkspaceCliCommand : Command
{
    public InitWorkspaceCliCommand(IMediator mediator) : base("init", "Register a directory as a workspace")
    {
        var pathOpt = new Option<string?>("--path", "Directory to initialise (defaults to cwd)");
        var nameOpt = new Option<string?>("--name", "Workspace name (defaults to directory name)");
        var noGitHubDetectOpt = new Option<bool>("--no-github-detect", "Skip auto-detecting GitHub remote");

        AddOption(pathOpt);
        AddOption(nameOpt);
        AddOption(noGitHubDetectOpt);

        this.SetHandler(async (string? path, string? name, bool noGitHubDetect) =>
        {
            var dir = path ?? Directory.GetCurrentDirectory();
            var result = await mediator.Send(new InitWorkspaceCommand(dir, name, DetectGitHub: !noGitHubDetect));
            var ws = result.Workspace;
            if (result.Created)
                Console.WriteLine($"Initialized workspace '{ws.Name}' at {ws.Path}");
            else
                Console.WriteLine($"Workspace '{ws.Name}' is already initialized");
            if (result.GitHubLinked)
                Console.WriteLine($"  GitHub: {ws.GitHubRepo}");
        }, pathOpt, nameOpt, noGitHubDetectOpt);
    }
}

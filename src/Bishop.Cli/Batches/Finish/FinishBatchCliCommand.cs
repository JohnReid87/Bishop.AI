using Bishop.App.Batches.FinishBatch;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Batches.Finish;

internal sealed class FinishBatchCliCommand : Command
{
    public FinishBatchCliCommand(ISender mediator) : base("finish", "Push the batch branch and open a PR; batch stays open until 'bishop batch complete'")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string name, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);

            if (ws.GitHubRepo is null)
                throw new InvalidOperationException(
                    $"Workspace '{ws.Name}' has no GitHub repo configured. Run: bishop workspace set-github <owner/repo>");

            var result = await mediator.Send(new FinishBatchCommand(name, ws.Path, ws.GitHubRepo));
            Console.WriteLine($"PR: {result.PrUrl}");
        }, nameArg, CommonOptions.WorkspaceOption);
    }
}

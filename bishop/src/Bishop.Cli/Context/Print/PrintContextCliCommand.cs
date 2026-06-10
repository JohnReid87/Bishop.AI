using Bishop.App.Context;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Context.Print;

internal sealed class PrintContextCliCommand : Command
{
    public PrintContextCliCommand(ISender mediator)
        : base("print", "Print the workspace context file, or a single named section")
    {
        var resolver = new WorkspaceResolver(mediator);
        var sectionOpt = new Option<string?>("--section", "Print only this H2 section by name");

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(sectionOpt);

        this.SetHandler(async (string? workspace, string? section) =>
        {
            try
            {
                var ws = await resolver.ResolveAsync(workspace);
                var output = await mediator.Send(new PrintContextQuery(ws.Path, section));
                Console.WriteLine(output);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, CommonOptions.WorkspaceOption, sectionOpt);
    }
}

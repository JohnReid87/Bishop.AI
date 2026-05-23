using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Workspaces.Current;

internal sealed class CurrentWorkspaceCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public CurrentWorkspaceCliCommand(IMediator mediator)
        : base("current", "Show the workspace whose path is an ancestor of cwd")
    {
        var resolver = new WorkspaceResolver(mediator);
        AddOption(CommonOptions.JsonOption);
        this.SetHandler(async (bool json) =>
        {
            try
            {
                var ws = await resolver.ResolveAsync(null);
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(ws, s_jsonOpts));
                else
                    Console.WriteLine($"{ws.Name,-30} {ws.Path}");
            }
            catch (InvalidOperationException)
            {
                Environment.ExitCode = 1;
            }
        }, CommonOptions.JsonOption);
    }
}

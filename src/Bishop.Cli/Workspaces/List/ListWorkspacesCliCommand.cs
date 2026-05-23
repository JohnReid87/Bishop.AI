using Bishop.App.Workspaces.ListWorkspaces;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Workspaces.List;

internal sealed class ListWorkspacesCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ListWorkspacesCliCommand(IMediator mediator) : base("list", "List all workspaces")
    {
        AddOption(CommonOptions.JsonOption);
        this.SetHandler(async (bool json) =>
        {
            var workspaces = await mediator.Send(new ListWorkspacesQuery());
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(workspaces.OrderBy(w => w.Position), s_jsonOpts));
            else
                foreach (var w in workspaces.OrderBy(w => w.Position))
                    Console.WriteLine($"{w.Name,-30} {w.Path}");
        }, CommonOptions.JsonOption);
    }
}

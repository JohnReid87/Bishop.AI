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

    private static readonly Option<bool> s_includeRemovedOpt =
        new("--include-removed", "Include archived (removed) workspaces in the output");

    public ListWorkspacesCliCommand(IMediator mediator) : base("list", "List all workspaces")
    {
        AddOption(CommonOptions.JsonOption);
        AddOption(s_includeRemovedOpt);
        this.SetHandler(async (bool json, bool includeRemoved) =>
        {
            var workspaces = await mediator.Send(new ListWorkspacesQuery(includeRemoved));
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(workspaces.OrderBy(w => w.Position), s_jsonOpts));
            else
                foreach (var w in workspaces.OrderBy(w => w.Position))
                {
                    var name = w.IsRemoved ? $"{w.Name} [removed]" : w.Name;
                    Console.WriteLine($"{name,-30} {w.Path}");
                }
        }, CommonOptions.JsonOption, s_includeRemovedOpt);
    }
}

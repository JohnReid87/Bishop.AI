using Bishop.App.Lanes.ListLanesByWorkspace;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Lanes.List;

internal sealed class ListLanesCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ListLanesCliCommand(IMediator mediator) : base("list", "List lanes in a workspace")
    {
        var resolver = new WorkspaceResolver(mediator);

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (string? workspace, bool json) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(lanes, s_jsonOpts));
            else
                foreach (var l in lanes)
                    Console.WriteLine($"  {l.Position}  {l.Name}");
        }, CommonOptions.WorkspaceOption, CommonOptions.JsonOption);
    }
}

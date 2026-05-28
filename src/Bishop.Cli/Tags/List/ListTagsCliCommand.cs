using Bishop.App.Tags.ListTagsByWorkspace;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Tags.List;

internal sealed class ListTagsCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ListTagsCliCommand(ISender mediator) : base("list", "List tags in a workspace")
    {
        var resolver = new WorkspaceResolver(mediator);

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (string? workspace, bool json) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var tags = await mediator.Send(new ListTagsByWorkspaceQuery(ws.Id));
            if (json)
                Console.WriteLine(JsonSerializer.Serialize(tags, s_jsonOpts));
            else
                foreach (var t in tags)
                    Console.WriteLine(t.Name);
        }, CommonOptions.WorkspaceOption, CommonOptions.JsonOption);
    }
}

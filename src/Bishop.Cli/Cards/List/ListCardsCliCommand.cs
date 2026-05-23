using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Cards.List;

internal sealed class ListCardsCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ListCardsCliCommand(IMediator mediator) : base("list", "List cards in a workspace")
    {
        var resolver = new WorkspaceResolver(mediator);

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (string? workspace, bool json) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var cards = await mediator.Send(new ListCardsByWorkspaceQuery(ws.Id));
            var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
            var lanePositionByName = lanes.ToDictionary(l => l.Name, l => l.Position, StringComparer.OrdinalIgnoreCase);

            if (json)
            {
                var output = cards.Select(c => new
                {
                    id = c.Id,
                    number = c.Number,
                    title = c.Title,
                    description = c.Description,
                    laneName = c.LaneName,
                    position = c.Position,
                    isClosed = c.IsClosed,
                    gitHubIssueNumber = c.GitHubIssueNumber,
                    gitHubPushedAt = c.GitHubPushedAt,
                    tag = c.TagName
                });
                Console.WriteLine(JsonSerializer.Serialize(output, s_jsonOpts));
            }
            else
            {
                var grouped = cards
                    .GroupBy(c => c.LaneName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (
                        name: g.Key,
                        position: lanePositionByName.TryGetValue(g.Key, out var p) ? p : int.MaxValue,
                        cards: g.OrderBy(c => c.Position).ToList()))
                    .OrderBy(t => t.position);

                foreach (var (name, _, laneCards) in grouped)
                {
                    Console.WriteLine($"\n[{name}]");
                    foreach (var c in laneCards)
                    {
                        var tagSuffix = c.TagName is not null ? $"  [{c.TagName}]" : "";
                        var closedMarker = c.IsClosed ? " [closed]" : "";
                        Console.WriteLine($"  #{c.Number,-4}  {c.Title}{closedMarker}{tagSuffix}");
                    }
                }
            }
        }, CommonOptions.WorkspaceOption, CommonOptions.JsonOption);
    }
}

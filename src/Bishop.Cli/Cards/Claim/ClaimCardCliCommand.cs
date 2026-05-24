using Bishop.App.Cards.ClaimCard;
using Bishop.Core;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Cards.Claim;

internal sealed class ClaimCardCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ClaimCardCliCommand(ISender mediator) : base("claim", "Pick the top card from a lane and move it to Doing")
    {
        var resolver = new WorkspaceResolver(mediator);
        var claimSourceLaneOpt = new Option<string>("--lane", () => SystemLaneNames.ToDo, "Source lane to claim from");
        var claimTagOpt = new Option<string?>("--tag", "Only claim the first card carrying this tag");

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(claimSourceLaneOpt);
        AddOption(claimTagOpt);
        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (string? workspace, string sourceLaneName, string? tagName, bool json) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var card = await mediator.Send(new ClaimCardCommand(ws.Id, sourceLaneName, tagName));

            if (card is null)
            {
                Console.Error.WriteLine(tagName is null
                    ? $"Lane '{sourceLaneName}' is empty — nothing to claim."
                    : $"No card tagged '{tagName}' in '{sourceLaneName}'.");
                Environment.ExitCode = 1;
                return;
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    id = card.Id,
                    number = card.Number,
                    title = card.Title,
                    description = card.Description,
                    laneName = card.LaneName,
                    position = card.Position,
                    createdAt = card.CreatedAt,
                    updatedAt = card.UpdatedAt,
                    tag = card.TagName
                }, s_jsonOpts));
            }
            else
            {
                Console.WriteLine($"Claimed #{card.Number} — '{card.Title}' [{sourceLaneName}] → [{card.LaneName}]");
                if (card.TagName is not null)
                    Console.WriteLine($"Tag: {card.TagName}");
                if (!string.IsNullOrEmpty(card.Description))
                {
                    Console.WriteLine();
                    Console.WriteLine(card.Description);
                }
            }
        }, CommonOptions.WorkspaceOption, claimSourceLaneOpt, claimTagOpt, CommonOptions.JsonOption);
    }
}

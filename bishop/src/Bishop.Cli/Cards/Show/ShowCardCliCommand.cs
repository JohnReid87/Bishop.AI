using Bishop.App.Cards.GetCard;
using Bishop.App.Services.Claude;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Cards.Show;

internal sealed class ShowCardCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ShowCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("show", "Show details of a card")
    {
        var cardViewIdArg = new Argument<string>("card-id", "Card short ID or prefix");

        AddArgument(cardViewIdArg);
        AddOption(CommonOptions.WorkspaceOption);
        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (string prefix, string? workspace, bool json) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, _, ws) = resolved.Value;

            var card = await mediator.Send(new GetCardQuery(cardId))
                ?? throw new InvalidOperationException($"Card {cardId} not found.");

            if (json)
            {
                var gitCommit = await mediator.Send(new GetCardCommitQuery(card.Number, ws.Path));
                object? commitObj = gitCommit is GetCardCommitResult.Found found
                    ? new
                    {
                        hash = found.Commit.FullHash,
                        shortHash = found.Commit.ShortHash,
                        isPushed = found.Commit.IsPushed,
                    }
                    : null;

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    id = card.Id,
                    number = card.Number,
                    title = card.Title,
                    description = card.Description,
                    laneName = card.LaneName,
                    position = card.Position,
                    isClosed = card.IsClosed,
                    createdAt = card.CreatedAt,
                    updatedAt = card.UpdatedAt,
                    totalInputTokens = card.TotalInputTokens,
                    totalOutputTokens = card.TotalOutputTokens,
                    claudeRunCount = card.ClaudeRunCount,
                    lastAutoRunFailedAt = card.LastAutoRunFailedAt,
                    lastAutoRunSucceededAt = card.LastAutoRunSucceededAt,
                    tag = card.TagName,
                    commit = commitObj
                }, s_jsonOpts));
            }
            else
            {
                Console.WriteLine(card.Title);
                Console.WriteLine($"Lane: {card.LaneName}");
                if (card.IsClosed)
                    Console.WriteLine("Status: closed");
                if (card.TagName is not null)
                    Console.WriteLine($"Tag: {card.TagName}");
                var claudeLine = ClaudeTotalsFormatter.Format(
                    card.TotalInputTokens,
                    card.TotalOutputTokens,
                    card.ClaudeRunCount);
                if (claudeLine is not null)
                    Console.WriteLine(claudeLine);
                if (!string.IsNullOrEmpty(card.Description))
                {
                    Console.WriteLine();
                    Console.WriteLine(card.Description);
                }
            }
        }, cardViewIdArg, CommonOptions.WorkspaceOption, CommonOptions.JsonOption);
    }
}

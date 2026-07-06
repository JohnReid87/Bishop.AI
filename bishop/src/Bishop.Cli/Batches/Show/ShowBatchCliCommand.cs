using Bishop.App.Batches.GetBatch;
using Bishop.Core;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Batches.Show;

internal sealed class ShowBatchCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ShowBatchCliCommand(ISender mediator) : base("show", "Show batch details and card list")
    {
        var nameArg = new Argument<string>("name", "Batch name");

        AddArgument(nameArg);
        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (string name, bool json) =>
        {
            var result = await mediator.Send(new GetBatchQuery(name));

            var displayState = BatchDisplayStates.Derive(
                result.Batch.Status,
                result.Batch.FinishedAt,
                result.Batch.MergedAt,
                result.Cards.Count > 0 && result.Cards.All(c => c.LaneName == SystemLaneNames.Done))
                .ToString();

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    id = result.Batch.Id,
                    name = result.Batch.Name,
                    branchName = result.Batch.BranchName,
                    baseBranch = result.Batch.BaseBranch,
                    status = result.Batch.Status.ToString(),
                    displayState,
                    worktreePath = result.Batch.WorktreePath,
                    createdAt = result.Batch.CreatedAt,
                    closedAt = result.Batch.ClosedAt,
                    cards = result.Cards.Select(c => new
                    {
                        id = c.Id,
                        number = c.Number,
                        title = c.Title,
                        laneName = c.LaneName,
                        tag = c.TagName
                    })
                }, s_jsonOpts));
                return;
            }

            Console.WriteLine($"Batch:    {result.Batch.Name}");
            Console.WriteLine($"Branch:   {result.Batch.BranchName}");
            Console.WriteLine($"Base:     {result.Batch.BaseBranch}");
            Console.WriteLine($"Status:   {displayState}");
            Console.WriteLine($"Worktree: {result.Batch.WorktreePath}");

            if (result.Cards.Count == 0)
            {
                Console.WriteLine("\nCards: (none)");
                return;
            }

            Console.WriteLine($"\nCards ({result.Cards.Count}):");
            foreach (var c in result.Cards)
            {
                var tagSuffix = c.TagName is not null ? $"  [{c.TagName}]" : "";
                Console.WriteLine($"  #{c.Number,-4}  {c.Title,-40}  {c.LaneName}{tagSuffix}");
            }
        }, nameArg, CommonOptions.JsonOption);
    }
}

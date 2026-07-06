using Bishop.App.Batches.ListBatches;
using Bishop.Core;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Batches.List;

internal sealed class ListBatchesCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ListBatchesCliCommand(ISender mediator) : base("list", "List non-Closed batches")
    {
        var resolver = new WorkspaceResolver(mediator);
        AddOption(CommonOptions.JsonOption);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (bool json, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var summaries = await mediator.Send(new ListBatchesQuery(ws.Id, ws.Path));

            if (json)
            {
                var output = summaries.Select(s => new
                {
                    id = s.Batch.Id,
                    name = s.Batch.Name,
                    branchName = s.Batch.BranchName,
                    baseBranch = s.Batch.BaseBranch,
                    status = s.Batch.Status.ToString(),
                    displayState = DeriveDisplayState(s),
                    worktreePath = s.Batch.WorktreePath,
                    createdAt = s.Batch.CreatedAt,
                    cardCount = s.CardCount
                });
                Console.WriteLine(JsonSerializer.Serialize(output, s_jsonOpts));
                return;
            }

            if (summaries.Count == 0)
            {
                Console.WriteLine("No active batches.");
                return;
            }

            var nameWidth = Math.Max(4, summaries.Max(s => s.Batch.Name.Length));
            var branchWidth = Math.Max(6, summaries.Max(s => s.Batch.BranchName.Length));
            var statusWidth = Math.Max(8, summaries.Max(s => DeriveDisplayState(s).Length));

            Console.WriteLine(
                $"{"Name".PadRight(nameWidth)}  {"Branch".PadRight(branchWidth)}  {"Status".PadRight(statusWidth)}  Cards");
            Console.WriteLine(
                $"{new string('-', nameWidth)}  {new string('-', branchWidth)}  {new string('-', statusWidth)}  -----");

            foreach (var s in summaries)
            {
                Console.WriteLine(
                    $"{s.Batch.Name.PadRight(nameWidth)}  {s.Batch.BranchName.PadRight(branchWidth)}  {DeriveDisplayState(s).PadRight(statusWidth)}  {s.CardCount}");
            }
        }, CommonOptions.JsonOption, CommonOptions.WorkspaceOption);
    }

    private static string DeriveDisplayState(BatchSummary summary) =>
        BatchDisplayStates.Derive(
            summary.Batch.Status,
            summary.Batch.FinishedAt,
            summary.Batch.MergedAt,
            summary.Cards.Count > 0 && summary.Cards.All(c => c.LaneName == SystemLaneNames.Done))
            .ToString();
}

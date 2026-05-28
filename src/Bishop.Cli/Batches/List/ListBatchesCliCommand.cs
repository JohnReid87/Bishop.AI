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

            Console.WriteLine(
                $"{"Name".PadRight(nameWidth)}  {"Branch".PadRight(branchWidth)}  {"Status".PadRight(7)}  Cards");
            Console.WriteLine(
                $"{new string('-', nameWidth)}  {new string('-', branchWidth)}  {"-------"}  -----");

            foreach (var s in summaries)
            {
                Console.WriteLine(
                    $"{s.Batch.Name.PadRight(nameWidth)}  {s.Batch.BranchName.PadRight(branchWidth)}  {s.Batch.Status.ToString().PadRight(7)}  {s.CardCount}");
            }
        }, CommonOptions.JsonOption, CommonOptions.WorkspaceOption);
    }
}

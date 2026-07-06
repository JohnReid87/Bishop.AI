using Bishop.App.Batches.GetBatch;
using Bishop.App.Findings.GetPriorFindings;
using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

internal sealed class ReviewBatchContextProvider : IContextProvider
{
    public string SkillName => "review-batch";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Card model",
        "Card Push Procedure",
        "Per-finding Walk Pattern",
        "Findings Recording Procedure"
    };

    public async Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.Batch))
            throw new InvalidOperationException(
                "The review-batch context-pack requires --batch <name>.");

        var result = await mediator.Send(new GetBatchQuery(args.Batch), cancellationToken);
        var batch = result.Batch;

        var prior = await mediator.Send(
            new GetPriorFindingsQuery(workspace.Id, "bish-review-batch", batch.Id), cancellationToken);

        return new
        {
            batch = new
            {
                name = batch.Name,
                branchName = batch.BranchName,
                baseBranch = batch.BaseBranch,
                worktreePath = batch.WorktreePath,
                status = batch.Status.ToString(),
            },
            cards = result.Cards.Select(c => new
            {
                number = c.Number,
                title = c.Title,
                description = c.Description,
                tag = c.TagName,
                laneName = c.LaneName,
                isClosed = c.IsClosed,
            }),
            priorFindings = prior,
        };
    }
}

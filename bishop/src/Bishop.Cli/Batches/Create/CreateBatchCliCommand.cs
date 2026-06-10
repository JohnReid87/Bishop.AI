using Bishop.App.Batches.CreateBatch;
using Bishop.App.Skills;
using MediatR;
using System.CommandLine;
using System.Text.RegularExpressions;

namespace Bishop.Cli.Batches.Create;

internal sealed class CreateBatchCliCommand : Command
{
    public CreateBatchCliCommand(ISender mediator) : base("create", "Create a batch and provision its worktree")
    {
        var resolver = new WorkspaceResolver(mediator);
        var nameOpt = new Option<string>("--name", "Batch name") { IsRequired = true };
        var branchOpt = new Option<string?>("--branch", "Branch name (default: bishop/<slug>)");
        var baseOpt = new Option<string?>("--base", "Base branch (default: current branch)");
        var cardsOpt = new Option<string?>("--cards", "Comma-separated card numbers to assign");
        var tagOpt = new Option<string?>("--tag", "Assign matching cards by tag");
        var laneOpt = new Option<string?>("--lane", "Assign matching cards by lane");
        var modelOpt = new Option<string>("--model", () => SkillModelOptions.DefaultModelId, "Claude model ID persisted on the batch");

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(nameOpt);
        AddOption(branchOpt);
        AddOption(baseOpt);
        AddOption(cardsOpt);
        AddOption(tagOpt);
        AddOption(laneOpt);
        AddOption(modelOpt);

        this.SetHandler(
            async (string? workspace, string name, string? branch, string? @base, string? cards, string? tag, string? lane, string model) =>
            {
                var ws = await resolver.ResolveAsync(workspace);
                var slug = Slugify(name);
                var branchName = branch ?? $"bishop/{slug}";

                var repoName = Path.GetFileName(ws.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var parentDir = Path.GetDirectoryName(ws.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!;
                var worktreePath = Path.Combine(parentDir, $"{repoName}-bishop-worktrees", slug);

                int[] cardNumbers = [];
                if (cards is not null)
                {
                    cardNumbers = cards.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s.Trim()))
                        .ToArray();
                }

                var result = await mediator.Send(new CreateBatchCommand(
                    ws.Id, ws.Path, name, branchName, @base, worktreePath, cardNumbers, tag, lane, model));

                Console.WriteLine($"Created batch '{result.Batch.Name}'");
                Console.WriteLine($"Branch:   {result.Batch.BranchName}");
                Console.WriteLine($"Base:     {result.Batch.BaseBranch}");
                Console.WriteLine($"Worktree: {result.Batch.WorktreePath}");
                if (result.CardCount > 0)
                    Console.WriteLine($"Cards:    {result.CardCount} assigned");
            },
            CommonOptions.WorkspaceOption, nameOpt, branchOpt, baseOpt, cardsOpt, tagOpt, laneOpt, modelOpt);
    }

    private static string Slugify(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(' ', '-'), "[^a-z0-9-]", "");
}

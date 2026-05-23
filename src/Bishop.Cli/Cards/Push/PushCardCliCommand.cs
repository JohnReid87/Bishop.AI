using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.PushLane;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Push;

internal sealed class PushCardCliCommand : Command
{
    public PushCardCliCommand(IMediator mediator, CardResolver cardResolver)
        : base("push", "Push a card to GitHub Issues")
    {
        var resolver = new WorkspaceResolver(mediator);
        var cardPushIdArg = new Argument<string?>("card-id", "Card short ID or prefix (mutually exclusive with --lane)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
        var cardPushLaneOpt = new Option<string?>("--lane", "Push all unlinked cards in a lane (mutually exclusive with card-id)");
        var cardPushDryRunOpt = new Option<bool>("--dry-run", "Preview what would be pushed without calling gh");

        AddArgument(cardPushIdArg);
        AddOption(cardPushLaneOpt);
        AddOption(cardPushDryRunOpt);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string? prefix, string? lane, bool dryRun, string? workspace) =>
        {
            if (prefix is not null && lane is not null)
            {
                Console.Error.WriteLine("card-id and --lane are mutually exclusive.");
                Environment.ExitCode = 1;
                return;
            }
            if (prefix is null && lane is null)
            {
                Console.Error.WriteLine("Specify either a card-id or --lane <name>.");
                Environment.ExitCode = 1;
                return;
            }
            if (lane is not null)
            {
                var ws = await resolver.ResolveAsync(workspace);
                var result = await mediator.Send(new PushLaneCommand(ws.Id, lane, dryRun));
                var dryPrefix = dryRun ? "[dry-run] " : string.Empty;
                Console.WriteLine($"{dryPrefix}pushed {result.Pushed.Count}, skipped {result.SkippedAlreadyLinked} (already linked), failed {result.Failed.Count}.");
                foreach (var c in result.Pushed)
                {
                    var issueRef = dryRun ? string.Empty : $"  https://github.com/{ws.GitHubRepo}/issues/{c.GitHubIssueNumber}";
                    Console.WriteLine($"  {(dryRun ? "would push" : "pushed")}  #{c.Number}  {c.Title}{issueRef}");
                }
                foreach (var f in result.Failed)
                    Console.WriteLine($"  failed  #{f.CardNumber}  {f.Error}");
                if (result.Failed.Count > 0)
                    Environment.ExitCode = 1;
                return;
            }
            var resolved = await cardResolver.ResolveAsync(workspace, prefix!);
            if (resolved is null) return;
            var (cardId, _, wsResolved) = resolved.Value;
            var card = await mediator.Send(new PushCardCommand(cardId));
            var singleIssueUrl = $"https://github.com/{wsResolved.GitHubRepo}/issues/{card.GitHubIssueNumber}";
            Console.WriteLine($"Pushed card #{card.Number} → {singleIssueUrl}");
        }, cardPushIdArg, cardPushLaneOpt, cardPushDryRunOpt, CommonOptions.WorkspaceOption);
    }
}

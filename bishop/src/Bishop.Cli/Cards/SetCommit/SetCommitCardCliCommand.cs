using Bishop.App.Cards.SetCardCommit;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.SetCommit;

internal sealed class SetCommitCardCliCommand : Command
{
    public SetCommitCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("set-commit", "Record the commit hash and branch for a card")
    {
        var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");
        var hashOpt = new Option<string>("--hash", "Full commit SHA") { IsRequired = true };
        var branchOpt = new Option<string>("--branch", "Branch name") { IsRequired = true };

        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);
        AddOption(hashOpt);
        AddOption(branchOpt);

        this.SetHandler(async (string prefix, string? workspace, string hash, string branch) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, cardNumber, _) = resolved.Value;
            await mediator.Send(new SetCardCommitCommand(cardId, hash, branch));
            Console.WriteLine($"Recorded commit for card #{cardNumber}: {hash[..Math.Min(8, hash.Length)]} on {branch}");
        }, cardIdArg, CommonOptions.WorkspaceOption, hashOpt, branchOpt);
    }
}

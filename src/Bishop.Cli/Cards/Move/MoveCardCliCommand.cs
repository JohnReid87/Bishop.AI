using Bishop.App.Cards.MoveCard;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Move;

internal sealed class MoveCardCliCommand : Command
{
    public MoveCardCliCommand(IMediator mediator, CardResolver cardResolver)
        : base("move", "Move a card to another lane or position")
    {
        var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");
        var toLaneOpt = new Option<string>("--to-lane", "Target lane name") { IsRequired = true };
        var toPositionOpt = new Option<int>("--to-position", "Target zero-based position") { IsRequired = true };
        var noCloseOpt = new Option<bool>("--no-close", "Skip auto-close when moving into the Done lane");

        AddArgument(cardIdArg);
        AddOption(CommonOptions.WorkspaceOption);
        AddOption(toLaneOpt);
        AddOption(toPositionOpt);
        AddOption(noCloseOpt);

        this.SetHandler(async (string prefix, string? workspace, string toLane, int toPosition, bool noClose) =>
        {
            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, _, _) = resolved.Value;
            var card = await mediator.Send(new MoveCardCommand(cardId, toLane, toPosition, noClose));
            Console.WriteLine($"Moved card #{card.Number} → [{card.LaneName}] position {card.Position}");
        }, cardIdArg, CommonOptions.WorkspaceOption, toLaneOpt, toPositionOpt, noCloseOpt);
    }
}

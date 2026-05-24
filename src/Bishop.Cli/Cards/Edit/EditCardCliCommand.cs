using Bishop.App.Cards.UpdateCard;
using MediatR;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Bishop.Cli.Cards.Edit;

internal sealed class EditCardCliCommand : Command
{
    public EditCardCliCommand(ISender mediator, CardResolver cardResolver)
        : base("edit", "Edit a card's title, description, or tag")
    {
        var cardEditIdArg = new Argument<string>("card-id", "Card short ID or prefix");
        var editTitleOpt = new Option<string?>("--title", "New card title");
        var editDescOpt = new Option<string?>("--description", "New card description");
        var editDescFileOpt = new Option<string?>("--description-file", "Read description from file (use - for stdin)");
        var editAppendDescFileOpt = new Option<string?>("--append-description-file", "Append to description from file (use - for stdin); mutually exclusive with --description and --description-file");
        var editTagOpt = new Option<string?>("--tag", "Set tag (use empty string to clear)");
        var editToLaneOpt = new Option<string?>("--to-lane", "Move card to this lane after editing");
        var editNoCloseOpt = new Option<bool>("--no-close", "Skip auto-close when moving into the Done lane");

        AddArgument(cardEditIdArg);
        AddOption(CommonOptions.WorkspaceOption);
        AddOption(editTitleOpt);
        AddOption(editDescOpt);
        AddOption(editDescFileOpt);
        AddOption(editAppendDescFileOpt);
        AddOption(editTagOpt);
        AddOption(editToLaneOpt);
        AddOption(editNoCloseOpt);

        this.SetHandler(async (InvocationContext ctx) =>
        {
            var prefix = ctx.ParseResult.GetValueForArgument(cardEditIdArg);
            var workspace = ctx.ParseResult.GetValueForOption(CommonOptions.WorkspaceOption);
            var title = ctx.ParseResult.GetValueForOption(editTitleOpt);
            var description = ctx.ParseResult.GetValueForOption(editDescOpt);
            var descFile = ctx.ParseResult.GetValueForOption(editDescFileOpt);
            var appendDescFile = ctx.ParseResult.GetValueForOption(editAppendDescFileOpt);
            var tag = ctx.ParseResult.GetValueForOption(editTagOpt);
            var toLane = ctx.ParseResult.GetValueForOption(editToLaneOpt);
            var noClose = ctx.ParseResult.GetValueForOption(editNoCloseOpt);

            var descOptionCount = new[] { description is not null, descFile is not null, appendDescFile is not null }.Count(x => x);
            if (descOptionCount > 1)
            {
                Console.Error.WriteLine("--description, --description-file, and --append-description-file are mutually exclusive.");
                Environment.ExitCode = 1;
                return;
            }

            var resolved = await cardResolver.ResolveAsync(workspace, prefix);
            if (resolved is null) return;
            var (cardId, _, _) = resolved.Value;

            var desc = descFile switch
            {
                "-" => await Console.In.ReadToEndAsync(),
                not null => await File.ReadAllTextAsync(descFile),
                null => description
            };

            string? appendDesc = null;
            if (appendDescFile is not null)
            {
                appendDesc = appendDescFile == "-"
                    ? await Console.In.ReadToEndAsync()
                    : await File.ReadAllTextAsync(appendDescFile);
            }

            var updateTag = tag is not null;
            var card = await mediator.Send(new UpdateCardCommand(cardId, title, desc, updateTag, tag, appendDesc, toLane, noClose));
            Console.WriteLine($"Updated card #{card.Number} — '{card.Title}'");
        });
    }
}

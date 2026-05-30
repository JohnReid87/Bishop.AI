using Bishop.App.Cards.AddCard;
using Bishop.Core;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Cards.Add;

internal sealed class AddCardCliCommand : Command
{
    public AddCardCliCommand(ISender mediator) : base("create", "Add a card to a lane")
    {
        var resolver = new WorkspaceResolver(mediator);
        var laneNameOpt = new Option<string>("--lane", "Lane name") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Card title") { IsRequired = true };
        var descOpt = new Option<string?>("--description", "Card description (optional)");
        var tagOpt = new Option<string?>("--tag", "Tag name");
        var descFileOpt = new Option<string?>("--description-file", "Read description from file (use - for stdin)");
        var bottomOpt = new Option<bool>("--bottom", "Insert at the bottom of the lane instead of the top");

        AddOption(CommonOptions.WorkspaceOption);
        AddOption(laneNameOpt);
        AddOption(titleOpt);
        AddOption(descOpt);
        AddOption(tagOpt);
        AddOption(descFileOpt);
        AddOption(bottomOpt);

        this.SetHandler(async (string? workspace, string lane, string title, string? description, string? tag, string? descFile, bool bottom) =>
        {
            var desc = await ResolveDescriptionAsync(descFile, description, Console.IsInputRedirected, Console.In);
            var ws = await resolver.ResolveAsync(workspace);
            var insertPosition = bottom ? CardInsertPosition.Bottom : CardInsertPosition.Top;
            var card = await mediator.Send(new AddCardCommand(ws.Id, lane, title, desc, tag, insertPosition));
            var tagSuffix = !string.IsNullOrEmpty(tag) ? $"  [{tag}]" : "";
            Console.WriteLine($"Added card #{card.Number} — '{card.Title}' → [{card.LaneName}]{tagSuffix}");
        }, CommonOptions.WorkspaceOption, laneNameOpt, titleOpt, descOpt, tagOpt, descFileOpt, bottomOpt);
    }

    // Resolves the card description from the available sources.
    // Precedence: --description-file (incl. "-" for stdin) > --description > redirected stdin > empty.
    internal static async Task<string> ResolveDescriptionAsync(
        string? descriptionFile, string? description, bool isInputRedirected, TextReader stdin) =>
        descriptionFile switch
        {
            "-" => await stdin.ReadToEndAsync(),
            not null => await File.ReadAllTextAsync(descriptionFile),
            null => description ?? (isInputRedirected ? await stdin.ReadToEndAsync() : "")
        };
}

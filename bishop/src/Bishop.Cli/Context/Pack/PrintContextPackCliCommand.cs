using Bishop.App.Context.ContextPack;
using MediatR;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Context.Pack;

internal sealed class PrintContextPackCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public PrintContextPackCliCommand(ISender mediator, IEnumerable<IContextProvider> providers)
        : base("context-pack", "Emit a pre-stuffed context bundle (workspace + git + skill-specific + conventions) as JSON")
    {
        var skillArg = new Argument<string?>("skill-name", () => null, "Registered provider name (e.g. work-on-card)");
        var cardOpt = new Option<int?>("--card", "Card number for skills that operate on a single card");
        var batchOpt = new Option<string?>("--batch", "Batch name for skills that operate on a single batch (e.g. review-batch)");
        var listOpt = new Option<bool>("--list", "List registered providers and exit");

        AddArgument(skillArg);
        AddOption(CommonOptions.WorkspaceOption);
        AddOption(cardOpt);
        AddOption(batchOpt);
        AddOption(listOpt);

        var providerList = providers.ToList();
        var resolver = new WorkspaceResolver(mediator);

        this.SetHandler(async (InvocationContext context) =>
        {
            var list = context.ParseResult.GetValueForOption(listOpt);
            if (list)
            {
                var entries = providerList
                    .OrderBy(p => p.SkillName, StringComparer.Ordinal)
                    .Select(p => new { name = p.SkillName, requiredSections = p.RequiredSections });
                Console.WriteLine(JsonSerializer.Serialize(new { providers = entries }, s_jsonOpts));
                return;
            }

            var skill = context.ParseResult.GetValueForArgument(skillArg);
            if (string.IsNullOrWhiteSpace(skill))
            {
                Console.Error.WriteLine("error: skill-name is required (or pass --list to enumerate providers).");
                context.ExitCode = 1;
                return;
            }

            var workspaceOption = context.ParseResult.GetValueForOption(CommonOptions.WorkspaceOption);
            var card = context.ParseResult.GetValueForOption(cardOpt);
            var batch = context.ParseResult.GetValueForOption(batchOpt);

            try
            {
                var ws = await resolver.ResolveAsync(workspaceOption);
                var pack = await mediator.Send(new BuildContextPackQuery(skill!, ws, new ContextPackArgs(card, batch)));
                Console.WriteLine(JsonSerializer.Serialize(pack, s_jsonOpts));
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                context.ExitCode = 1;
            }
        });
    }
}

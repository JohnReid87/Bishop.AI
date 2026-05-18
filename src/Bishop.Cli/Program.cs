using Bishop.App;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never
};

using var host = Host.CreateDefaultBuilder()
    .ConfigureServices(services => services.AddBishopApp(BishopDbConnectionString.Resolve()))
    .Build();

await host.StartAsync();

var mediator = host.Services.GetRequiredService<IMediator>();
var resolver = new WorkspaceResolver(mediator);

var root = new RootCommand("Bishop AI — kanban CLI");

// ── shared options ──────────────────────────────────────────────────────────

var workspaceOpt = new Option<string?>(
    aliases: ["--workspace", "-w"],
    description: "Workspace name or path (defaults to CWD ancestor match)");

var jsonOpt = new Option<bool>(
    name: "--json",
    description: "Emit JSON output");

// ── workspace list ──────────────────────────────────────────────────────────

var workspaceListCmd = new Command("list", "List all workspaces");
workspaceListCmd.AddOption(jsonOpt);
workspaceListCmd.SetHandler(async (bool json) =>
{
    var workspaces = await mediator.Send(new ListWorkspacesQuery());
    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(workspaces.OrderBy(w => w.Position), jsonOpts));
    }
    else
    {
        foreach (var w in workspaces.OrderBy(w => w.Position))
            Console.WriteLine($"{w.Name,-30} {w.Path}");
    }
}, jsonOpt);

// ── workspace current ───────────────────────────────────────────────────────

var workspaceCurrentCmd = new Command("current", "Show the workspace whose path is an ancestor of cwd");
workspaceCurrentCmd.AddOption(jsonOpt);
workspaceCurrentCmd.SetHandler(async (bool json) =>
{
    try
    {
        var ws = await resolver.ResolveAsync(null);
        if (json)
            Console.WriteLine(JsonSerializer.Serialize(ws, jsonOpts));
        else
            Console.WriteLine($"{ws.Name,-30} {ws.Path}");
    }
    catch (InvalidOperationException)
    {
        Environment.ExitCode = 1;
    }
}, jsonOpt);

var workspaceCmd = new Command("workspace", "Manage workspaces");
workspaceCmd.AddCommand(workspaceListCmd);
workspaceCmd.AddCommand(workspaceCurrentCmd);
root.AddCommand(workspaceCmd);

// ── card add ─────────────────────────────────────────────────────────────────

var laneNameOpt = new Option<string>("--lane", "Lane name") { IsRequired = true };
var titleOpt = new Option<string>("--title", "Card title") { IsRequired = true };
var descOpt = new Option<string?>("--description", "Card description (optional)");

var cardAddCmd = new Command("add", "Add a card to a lane");
cardAddCmd.AddOption(workspaceOpt);
cardAddCmd.AddOption(laneNameOpt);
cardAddCmd.AddOption(titleOpt);
cardAddCmd.AddOption(descOpt);
cardAddCmd.SetHandler(async (string? workspace, string lane, string title, string? description) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var targetLane = lanes.FirstOrDefault(l =>
        string.Equals(l.Name, lane, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{lane}' not found in workspace '{ws.Name}'.");
    var card = await mediator.Send(new AddCardCommand(targetLane.Id, title, description ?? ""));
    Console.WriteLine($"Added card {card.Id} — '{card.Title}' → [{targetLane.Name}]");
}, workspaceOpt, laneNameOpt, titleOpt, descOpt);

// ── card move ─────────────────────────────────────────────────────────────────

var cardIdArg = new Argument<Guid>("card-id", "Card GUID");
var toLaneOpt = new Option<string>("--to-lane", "Target lane name") { IsRequired = true };
var toPositionOpt = new Option<int>("--to-position", "Target zero-based position") { IsRequired = true };

var cardMoveCmd = new Command("move", "Move a card to another lane or position");
cardMoveCmd.AddArgument(cardIdArg);
cardMoveCmd.AddOption(workspaceOpt);
cardMoveCmd.AddOption(toLaneOpt);
cardMoveCmd.AddOption(toPositionOpt);
cardMoveCmd.SetHandler(async (Guid cardId, string? workspace, string toLane, int toPosition) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var targetLane = lanes.FirstOrDefault(l =>
        string.Equals(l.Name, toLane, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{toLane}' not found in workspace '{ws.Name}'.");
    var card = await mediator.Send(new MoveCardCommand(cardId, targetLane.Id, toPosition));
    Console.WriteLine($"Moved card {card.Id} → [{targetLane.Name}] position {card.Position}");
}, cardIdArg, workspaceOpt, toLaneOpt, toPositionOpt);

// ── card remove ───────────────────────────────────────────────────────────────

var cardRemoveIdArg = new Argument<Guid>("card-id", "Card GUID");

var cardRemoveCmd = new Command("remove", "Remove a card");
cardRemoveCmd.AddArgument(cardRemoveIdArg);
cardRemoveCmd.SetHandler(async (Guid cardId) =>
{
    await mediator.Send(new RemoveCardCommand(cardId));
    Console.WriteLine($"Removed card {cardId}");
}, cardRemoveIdArg);

// ── card list ─────────────────────────────────────────────────────────────────

var cardListCmd = new Command("list", "List cards in a workspace");
cardListCmd.AddOption(workspaceOpt);
cardListCmd.AddOption(jsonOpt);
cardListCmd.SetHandler(async (string? workspace, bool json) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var cards = await mediator.Send(new ListCardsByWorkspaceQuery(ws.Id));
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var laneById = lanes.ToDictionary(l => l.Id);

    if (json)
    {
        var output = cards.Select(c => new
        {
            id = c.Id,
            title = c.Title,
            description = c.Description,
            laneId = c.LaneId,
            laneName = laneById.TryGetValue(c.LaneId, out var l) ? l.Name : "",
            position = c.Position
        });
        Console.WriteLine(JsonSerializer.Serialize(output, jsonOpts));
    }
    else
    {
        var grouped = cards
            .GroupBy(c => c.LaneId)
            .Select(g => (
                lane: laneById.TryGetValue(g.Key, out var l) ? l : null,
                cards: g.OrderBy(c => c.Position).ToList()))
            .OrderBy(t => t.lane?.Position ?? int.MaxValue);

        foreach (var (lane, laneCards) in grouped)
        {
            Console.WriteLine($"\n[{lane?.Name ?? "?"}]");
            foreach (var c in laneCards)
                Console.WriteLine($"  {c.Id.ToString("N")[..8]}  {c.Title}");
        }
    }
}, workspaceOpt, jsonOpt);

// ── wire card command ─────────────────────────────────────────────────────────

var cardCmd = new Command("card", "Manage kanban cards");
cardCmd.AddCommand(cardAddCmd);
cardCmd.AddCommand(cardMoveCmd);
cardCmd.AddCommand(cardRemoveCmd);
cardCmd.AddCommand(cardListCmd);
root.AddCommand(cardCmd);

// ── run ───────────────────────────────────────────────────────────────────────

var parser = new CommandLineBuilder(root)
    .UseDefaults()
    .UseExceptionHandler((ex, _) =>
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        Environment.ExitCode = 1;
    })
    .Build();

var exitCode = await parser.InvokeAsync(args);
await host.StopAsync();
return exitCode;

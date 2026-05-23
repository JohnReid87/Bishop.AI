using Bishop.App;
using Bishop.App.Cards.AddCard;
using Bishop.App.Claude;
using Bishop.Core;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Lanes.AddLane;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Lanes.MoveLane;
using Bishop.App.Lanes.RemoveLane;
using Bishop.App.Lanes.RenameLane;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.SetWorkspaceGitHubRepo;
using Bishop.App.Workspaces.UnsetWorkspaceGitHubRepo;
using Bishop.App.Git;
using Bishop.App.WorkNext;
using Bishop.Cli;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.InputEncoding = new System.Text.UTF8Encoding(false);
Console.OutputEncoding = new System.Text.UTF8Encoding(false);

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    ReferenceHandler = ReferenceHandler.IgnoreCycles
};

var builder = Host.CreateEmptyApplicationBuilder(null);
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddBishopApp(BishopDbConnectionString.Resolve(), BishopStampPath.Resolve());
using var host = builder.Build();

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

// ── workspace init ──────────────────────────────────────────────────────────

var initPathOpt = new Option<string?>("--path", "Directory to initialise (defaults to cwd)");
var initNameOpt = new Option<string?>("--name", "Workspace name (defaults to directory name)");
var initNoTagsOpt = new Option<bool>("--no-tags", "Skip seeding canonical tags");
var initNoGitHubDetectOpt = new Option<bool>("--no-github-detect", "Skip auto-detecting GitHub remote");

var workspaceInitCmd = new Command("init", "Register a directory as a workspace and seed default lanes");
workspaceInitCmd.AddOption(initPathOpt);
workspaceInitCmd.AddOption(initNameOpt);
workspaceInitCmd.AddOption(initNoTagsOpt);
workspaceInitCmd.AddOption(initNoGitHubDetectOpt);
workspaceInitCmd.SetHandler(async (string? path, string? name, bool noTags, bool noGitHubDetect) =>
{
    var dir = path ?? Directory.GetCurrentDirectory();
    var result = await mediator.Send(new InitWorkspaceCommand(dir, name, SeedTags: !noTags, DetectGitHub: !noGitHubDetect));
    var ws = result.Workspace;
    if (result.Created)
    {
        Console.WriteLine($"Initialized workspace '{ws.Name}' at {ws.Path}");
        Console.WriteLine($"  Lanes: {string.Join(", ", result.LanesAdded)}");
    }
    else if (result.LanesAdded.Count > 0)
    {
        Console.WriteLine($"Workspace '{ws.Name}' already registered — added lanes: {string.Join(", ", result.LanesAdded)}");
    }
    else
    {
        Console.WriteLine($"Workspace '{ws.Name}' is already initialized");
    }
    if (result.TagsAdded.Count > 0)
        Console.WriteLine($"  Tags: {string.Join(", ", result.TagsAdded)}");
    if (result.GitHubLinked)
        Console.WriteLine($"  GitHub: {ws.GitHubRepo}");
}, initPathOpt, initNameOpt, initNoTagsOpt, initNoGitHubDetectOpt);

// ── workspace set-github ────────────────────────────────────────────────────

var setGithubRepoArg = new Argument<string>("repo", "GitHub repo in owner/repo format");

var workspaceSetGithubCmd = new Command("set-github", "Associate workspace with a GitHub repo");
workspaceSetGithubCmd.AddArgument(setGithubRepoArg);
workspaceSetGithubCmd.AddOption(workspaceOpt);
workspaceSetGithubCmd.SetHandler(async (string repo, string? workspace) =>
{
    if (!repo.Contains('/') || repo.StartsWith('/') || repo.EndsWith('/'))
    {
        Console.Error.WriteLine($"Invalid repo '{repo}': expected owner/repo format (e.g. JohnReid87/MyProject).");
        Environment.ExitCode = 1;
        return;
    }
    var ws = await resolver.ResolveAsync(workspace);
    await mediator.Send(new SetWorkspaceGitHubRepoCommand(ws.Id, repo));
    Console.WriteLine($"Workspace '{ws.Name}' linked to GitHub repo '{repo}'");
}, setGithubRepoArg, workspaceOpt);

// ── workspace unset-github ──────────────────────────────────────────────────

var workspaceUnsetGithubCmd = new Command("unset-github", "Remove the GitHub repo association from a workspace");
workspaceUnsetGithubCmd.AddOption(workspaceOpt);
workspaceUnsetGithubCmd.SetHandler(async (string? workspace) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    await mediator.Send(new UnsetWorkspaceGitHubRepoCommand(ws.Id));
    Console.WriteLine($"Removed GitHub repo association from workspace '{ws.Name}'");
}, workspaceOpt);

var workspaceCmd = new Command("workspace", "Manage workspaces");
workspaceCmd.AddCommand(workspaceListCmd);
workspaceCmd.AddCommand(workspaceCurrentCmd);
workspaceCmd.AddCommand(workspaceInitCmd);
workspaceCmd.AddCommand(workspaceSetGithubCmd);
workspaceCmd.AddCommand(workspaceUnsetGithubCmd);
root.AddCommand(workspaceCmd);

// ── card add ─────────────────────────────────────────────────────────────────

var laneNameOpt = new Option<string>("--lane", "Lane name") { IsRequired = true };
var titleOpt = new Option<string>("--title", "Card title") { IsRequired = true };
var descOpt = new Option<string?>("--description", "Card description (optional)");
var tagOpt = new Option<string?>("--tag", "Tag name");
var descFileOpt = new Option<string?>("--description-file", "Read description from file (use - for stdin)");

var bottomOpt = new Option<bool>("--bottom", "Insert at the bottom of the lane instead of the top");

var cardAddCmd = new Command("add", "Add a card to a lane");
cardAddCmd.AddOption(workspaceOpt);
cardAddCmd.AddOption(laneNameOpt);
cardAddCmd.AddOption(titleOpt);
cardAddCmd.AddOption(descOpt);
cardAddCmd.AddOption(tagOpt);
cardAddCmd.AddOption(descFileOpt);
cardAddCmd.AddOption(bottomOpt);
cardAddCmd.SetHandler(async (string? workspace, string lane, string title, string? description, string? tag, string? descFile, bool bottom) =>
{
    var desc = descFile switch
    {
        "-" => await Console.In.ReadToEndAsync(),
        not null => await File.ReadAllTextAsync(descFile),
        null => description ?? ""
    };
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var targetLane = lanes.FirstOrDefault(l =>
        string.Equals(l.Name, lane, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{lane}' not found in workspace '{ws.Name}'.");
    var insertPosition = bottom ? CardInsertPosition.Bottom : CardInsertPosition.Top;
    var card = await mediator.Send(new AddCardCommand(targetLane.Id, title, desc, tag, insertPosition));
    var tagSuffix = !string.IsNullOrEmpty(tag) ? $"  [{tag}]" : "";
    Console.WriteLine($"Added card #{card.Number} — '{card.Title}' → [{targetLane.Name}]{tagSuffix}");
}, workspaceOpt, laneNameOpt, titleOpt, descOpt, tagOpt, descFileOpt, bottomOpt);

// ── card short-ID prefix resolver ─────────────────────────────────────────────
// Returns null (exit code already set) for ambiguous prefix; throws for no match.

async Task<(Guid cardId, int cardNumber, Bishop.Core.Workspace ws)?> resolveCardByPrefixAsync(string? workspaceOption, string prefix)
{
    var ws = await resolver.ResolveAsync(workspaceOption);
    var stripped = prefix.TrimStart('#');

    if (stripped.Length > 0 && stripped.All(char.IsDigit) && int.TryParse(stripped, out var number))
    {
        var card = await mediator.Send(new GetCardByNumberQuery(number, ws.Id));
        if (card is null)
            throw new InvalidOperationException($"No card found matching '#{number}'.");
        return (card.Id, card.Number, ws);
    }

    var cards = await mediator.Send(new ListCardsByWorkspaceQuery(ws.Id));
    var matches = cards
        .Where(c => c.Id.ToString("N").StartsWith(stripped, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (matches.Count == 0)
        throw new InvalidOperationException($"No card found matching '{stripped}'.");
    if (matches.Count > 1)
    {
        Console.Error.WriteLine($"Ambiguous prefix '{stripped}' — {matches.Count} matches:");
        foreach (var c in matches)
            Console.Error.WriteLine($"  {c.Id.ToString("N")[..8]}  {c.Title}");
        Environment.ExitCode = 1;
        return null;
    }
    return (matches[0].Id, matches[0].Number, ws);
}

// ── card view ─────────────────────────────────────────────────────────────────

var cardViewIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardViewCmd = new Command("view", "Show details of a card");
cardViewCmd.AddArgument(cardViewIdArg);
cardViewCmd.AddOption(workspaceOpt);
cardViewCmd.AddOption(jsonOpt);
cardViewCmd.SetHandler(async (string prefix, string? workspace, bool json) =>
{
    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, _, ws) = resolved.Value;

    var card = await mediator.Send(new GetCardQuery(cardId))
        ?? throw new InvalidOperationException($"Card {cardId} not found.");

    if (json)
    {
        var gitCommit = await mediator.Send(new GetCardCommitQuery(card.Number, ws.Path));
        object? commitObj = gitCommit is GetCardCommitResult.Found found
            ? new
            {
                hash = found.Commit.FullHash,
                shortHash = found.Commit.ShortHash,
                isPushed = found.Commit.IsPushed,
                url = ws.GitHubRepo is not null
                    ? $"https://github.com/{ws.GitHubRepo}/commit/{found.Commit.FullHash}"
                    : (string?)null
            }
            : null;

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            id = card.Id,
            number = card.Number,
            title = card.Title,
            description = card.Description,
            laneId = card.LaneId,
            laneName = card.Lane.Name,
            position = card.Position,
            isClosed = card.IsClosed,
            gitHubIssueNumber = card.GitHubIssueNumber,
            gitHubPushedAt = card.GitHubPushedAt,
            createdAt = card.CreatedAt,
            updatedAt = card.UpdatedAt,
            totalInputTokens = card.TotalInputTokens,
            totalOutputTokens = card.TotalOutputTokens,
            claudeRunCount = card.ClaudeRunCount,
            tag = card.Tag?.Name,
            commit = commitObj
        }, jsonOpts));
    }
    else
    {
        Console.WriteLine(card.Title);
        Console.WriteLine($"Lane: {card.Lane.Name}");
        if (card.IsClosed)
            Console.WriteLine("Status: closed");
        if (card.Tag is not null)
            Console.WriteLine($"Tag: {card.Tag.Name}");
        var claudeLine = ClaudeTotalsFormatter.Format(
            card.TotalInputTokens,
            card.TotalOutputTokens,
            card.ClaudeRunCount);
        if (claudeLine is not null)
            Console.WriteLine(claudeLine);
        if (!string.IsNullOrEmpty(card.Description))
        {
            Console.WriteLine();
            Console.WriteLine(card.Description);
        }
    }
}, cardViewIdArg, workspaceOpt, jsonOpt);

// ── card move ─────────────────────────────────────────────────────────────────

var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");
var toLaneOpt = new Option<string>("--to-lane", "Target lane name") { IsRequired = true };
var toPositionOpt = new Option<int>("--to-position", "Target zero-based position") { IsRequired = true };
var noCloseOpt = new Option<bool>("--no-close", "Skip auto-close when moving into the Done lane");

var cardMoveCmd = new Command("move", "Move a card to another lane or position");
cardMoveCmd.AddArgument(cardIdArg);
cardMoveCmd.AddOption(workspaceOpt);
cardMoveCmd.AddOption(toLaneOpt);
cardMoveCmd.AddOption(toPositionOpt);
cardMoveCmd.AddOption(noCloseOpt);
cardMoveCmd.SetHandler(async (string prefix, string? workspace, string toLane, int toPosition, bool noClose) =>
{
    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, _, ws) = resolved.Value;
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var targetLane = lanes.FirstOrDefault(l =>
        string.Equals(l.Name, toLane, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{toLane}' not found in workspace '{ws.Name}'.");
    var card = await mediator.Send(new MoveCardCommand(cardId, targetLane.Id, toPosition, noClose));
    Console.WriteLine($"Moved card #{card.Number} → [{targetLane.Name}] position {card.Position}");
}, cardIdArg, workspaceOpt, toLaneOpt, toPositionOpt, noCloseOpt);

// ── card remove ───────────────────────────────────────────────────────────────

var cardRemoveIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardRemoveCmd = new Command("remove", "Remove a card");
cardRemoveCmd.AddArgument(cardRemoveIdArg);
cardRemoveCmd.AddOption(workspaceOpt);
cardRemoveCmd.SetHandler(async (string prefix, string? workspace) =>
{
    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, cardNumber, _) = resolved.Value;
    await mediator.Send(new RemoveCardCommand(cardId));
    Console.WriteLine($"Removed card #{cardNumber}");
}, cardRemoveIdArg, workspaceOpt);

// ── card edit ─────────────────────────────────────────────────────────────────

var cardEditIdArg = new Argument<string>("card-id", "Card short ID or prefix");
var editTitleOpt = new Option<string?>("--title", "New card title");
var editDescOpt = new Option<string?>("--description", "New card description");
var editDescFileOpt = new Option<string?>("--description-file", "Read description from file (use - for stdin)");
var editAppendDescFileOpt = new Option<string?>("--append-description-file", "Append to description from file (use - for stdin); mutually exclusive with --description and --description-file");
var editTagOpt = new Option<string?>("--tag", "Set tag (use empty string to clear)");
var editToLaneOpt = new Option<string?>("--to-lane", "Move card to this lane after editing");

var cardEditCmd = new Command("edit", "Edit a card's title, description, or tag");
cardEditCmd.AddArgument(cardEditIdArg);
cardEditCmd.AddOption(workspaceOpt);
cardEditCmd.AddOption(editTitleOpt);
cardEditCmd.AddOption(editDescOpt);
cardEditCmd.AddOption(editDescFileOpt);
cardEditCmd.AddOption(editAppendDescFileOpt);
cardEditCmd.AddOption(editTagOpt);
cardEditCmd.AddOption(editToLaneOpt);
cardEditCmd.SetHandler(async (string prefix, string? workspace, string? title, string? description, string? descFile, string? appendDescFile, string? tag, string? toLane) =>
{
    var descOptionCount = new[] { description is not null, descFile is not null, appendDescFile is not null }.Count(x => x);
    if (descOptionCount > 1)
    {
        Console.Error.WriteLine("--description, --description-file, and --append-description-file are mutually exclusive.");
        Environment.ExitCode = 1;
        return;
    }

    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, _, ws) = resolved.Value;

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

    Guid? toLaneId = null;
    if (toLane is not null)
    {
        var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
        var targetLane = lanes.FirstOrDefault(l =>
            string.Equals(l.Name, toLane, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Lane '{toLane}' not found in workspace '{ws.Name}'.");
        toLaneId = targetLane.Id;
    }

    var updateTag = tag is not null;
    var card = await mediator.Send(new UpdateCardCommand(cardId, title, desc, updateTag, tag, appendDesc, toLaneId));
    Console.WriteLine($"Updated card #{card.Number} — '{card.Title}'");
}, cardEditIdArg, workspaceOpt, editTitleOpt, editDescOpt, editDescFileOpt, editAppendDescFileOpt, editTagOpt, editToLaneOpt);

// ── card claim ────────────────────────────────────────────────────────────────

var claimSourceLaneOpt = new Option<string>("--lane", () => SystemLaneNames.ToDo, "Source lane to claim from");
var claimTagOpt = new Option<string?>("--tag", "Only claim the first card carrying this tag");

var cardClaimCmd = new Command("claim", "Pick the top card from a lane and move it to Doing");
cardClaimCmd.AddOption(workspaceOpt);
cardClaimCmd.AddOption(claimSourceLaneOpt);
cardClaimCmd.AddOption(claimTagOpt);
cardClaimCmd.AddOption(jsonOpt);
cardClaimCmd.SetHandler(async (string? workspace, string sourceLaneName, string? tagName, bool json) =>
{
    var ws = await resolver.ResolveAsync(workspace);

    var card = await mediator.Send(new ClaimCardCommand(ws.Id, sourceLaneName, tagName));

    if (card is null)
    {
        Console.Error.WriteLine(tagName is null
            ? $"Lane '{sourceLaneName}' is empty — nothing to claim."
            : $"No card tagged '{tagName}' in '{sourceLaneName}'.");
        Environment.ExitCode = 1;
        return;
    }

    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            id = card.Id,
            number = card.Number,
            title = card.Title,
            description = card.Description,
            laneId = card.LaneId,
            laneName = card.Lane.Name,
            position = card.Position,
            createdAt = card.CreatedAt,
            updatedAt = card.UpdatedAt,
            tag = card.Tag?.Name
        }, jsonOpts));
    }
    else
    {
        Console.WriteLine($"Claimed #{card.Number} — '{card.Title}' [{sourceLaneName}] → [{card.Lane.Name}]");
        if (card.Tag is not null)
            Console.WriteLine($"Tag: {card.Tag.Name}");
        if (!string.IsNullOrEmpty(card.Description))
        {
            Console.WriteLine();
            Console.WriteLine(card.Description);
        }
    }
}, workspaceOpt, claimSourceLaneOpt, claimTagOpt, jsonOpt);

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
            number = c.Number,
            title = c.Title,
            description = c.Description,
            laneId = c.LaneId,
            laneName = laneById.TryGetValue(c.LaneId, out var l) ? l.Name : "",
            position = c.Position,
            isClosed = c.IsClosed,
            gitHubIssueNumber = c.GitHubIssueNumber,
            gitHubPushedAt = c.GitHubPushedAt,
            tag = c.Tag?.Name
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
            {
                var tagSuffix = c.Tag is not null ? $"  [{c.Tag.Name}]" : "";
                var closedMarker = c.IsClosed ? " [closed]" : "";
                Console.WriteLine($"  #{c.Number,-4}  {c.Title}{closedMarker}{tagSuffix}");
            }
        }
    }
}, workspaceOpt, jsonOpt);

// ── tag list ──────────────────────────────────────────────────────────────────

var tagListCmd = new Command("list", "List tags in a workspace");
tagListCmd.AddOption(workspaceOpt);
tagListCmd.AddOption(jsonOpt);
tagListCmd.SetHandler(async (string? workspace, bool json) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var tags = await mediator.Send(new ListTagsByWorkspaceQuery(ws.Id));
    if (json)
        Console.WriteLine(JsonSerializer.Serialize(tags, jsonOpts));
    else
        foreach (var t in tags)
            Console.WriteLine(t.Name);
}, workspaceOpt, jsonOpt);

// ── wire tag command ──────────────────────────────────────────────────────────

var tagCmd = new Command("tag", "Manage workspace tags");
tagCmd.AddCommand(tagListCmd);
root.AddCommand(tagCmd);

// ── card push ─────────────────────────────────────────────────────────────────

var cardPushIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardPushCmd = new Command("push", "Push a card to GitHub Issues");
cardPushCmd.AddArgument(cardPushIdArg);
cardPushCmd.AddOption(workspaceOpt);
cardPushCmd.SetHandler(async (string prefix, string? workspace) =>
{
    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, _, ws) = resolved.Value;
    var card = await mediator.Send(new PushCardCommand(cardId));
    var issueUrl = $"https://github.com/{ws.GitHubRepo}/issues/{card.GitHubIssueNumber}";
    Console.WriteLine($"Pushed card #{card.Number} → {issueUrl}");
}, cardPushIdArg, workspaceOpt);

// ── card import-from-github ───────────────────────────────────────────────────

var importLabelOpt = new Option<string?>("--label", "Filter to issues carrying this GitHub label");
var importLimitOpt = new Option<int>("--limit", () => 100, "Maximum number of issues to import");
var importDryRunOpt = new Option<bool>("--dry-run", "Preview what would be imported without writing anything");

var cardImportFromGitHubCmd = new Command("import-from-github", "Import open GitHub issues as cards in the To Do lane");
cardImportFromGitHubCmd.AddOption(importLabelOpt);
cardImportFromGitHubCmd.AddOption(importLimitOpt);
cardImportFromGitHubCmd.AddOption(importDryRunOpt);
cardImportFromGitHubCmd.AddOption(jsonOpt);
cardImportFromGitHubCmd.AddOption(workspaceOpt);
cardImportFromGitHubCmd.SetHandler(async (string? label, int limit, bool dryRun, bool json, string? workspace) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var result = await mediator.Send(new ImportFromGitHubCommand(ws.Id, label, limit, dryRun));
    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOpts));
        return;
    }
    var prefix = dryRun ? "[dry-run] " : string.Empty;
    Console.WriteLine($"{prefix}Imported {result.Imported.Count}, skipped {result.SkippedAlreadyPresent.Count} (already present), failed {result.Failed.Count}.");
    foreach (var c in result.Imported)
        Console.WriteLine($"  {(dryRun ? "would import" : "imported")}  #{c.GitHubIssueNumber}  {c.Title}");
    foreach (var n in result.SkippedAlreadyPresent)
        Console.WriteLine($"  {(dryRun ? "would skip" : "skipped")}   #{n}");
    foreach (var f in result.Failed)
        Console.WriteLine($"  failed    #{f.IssueNumber}  {f.Error}");
}, importLabelOpt, importLimitOpt, importDryRunOpt, jsonOpt, workspaceOpt);

// ── card close ────────────────────────────────────────────────────────────────

var cardCloseIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardCloseCmd = new Command("close", "Mark a card as closed");
cardCloseCmd.AddArgument(cardCloseIdArg);
cardCloseCmd.AddOption(workspaceOpt);
cardCloseCmd.SetHandler(async (string prefix, string? workspace) =>
{
    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, cardNumber, _) = resolved.Value;
    await mediator.Send(new CloseCardCommand(cardId));
    Console.WriteLine($"Closed card #{cardNumber}");
}, cardCloseIdArg, workspaceOpt);

// ── card reopen ───────────────────────────────────────────────────────────────

var cardReopenIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardReopenCmd = new Command("reopen", "Reopen a closed card");
cardReopenCmd.AddArgument(cardReopenIdArg);
cardReopenCmd.AddOption(workspaceOpt);
cardReopenCmd.SetHandler(async (string prefix, string? workspace) =>
{
    var resolved = await resolveCardByPrefixAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, cardNumber, _) = resolved.Value;
    await mediator.Send(new ReopenCardCommand(cardId));
    Console.WriteLine($"Reopened card #{cardNumber}");
}, cardReopenIdArg, workspaceOpt);

// ── wire card command ─────────────────────────────────────────────────────────

var cardCmd = new Command("card", "Manage kanban cards");
cardCmd.AddCommand(cardAddCmd);
cardCmd.AddCommand(cardViewCmd);
cardCmd.AddCommand(cardMoveCmd);
cardCmd.AddCommand(cardRemoveCmd);
cardCmd.AddCommand(cardEditCmd);
cardCmd.AddCommand(cardClaimCmd);
cardCmd.AddCommand(cardListCmd);
cardCmd.AddCommand(cardPushCmd);
cardCmd.AddCommand(cardImportFromGitHubCmd);
cardCmd.AddCommand(cardCloseCmd);
cardCmd.AddCommand(cardReopenCmd);
root.AddCommand(cardCmd);

// ── lane list ─────────────────────────────────────────────────────────────────

var laneListCmd = new Command("list", "List lanes in a workspace");
laneListCmd.AddOption(workspaceOpt);
laneListCmd.AddOption(jsonOpt);
laneListCmd.SetHandler(async (string? workspace, bool json) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    if (json)
        Console.WriteLine(JsonSerializer.Serialize(lanes, jsonOpts));
    else
        foreach (var l in lanes)
        {
            var marker = l.IsSystem ? " [system]" : string.Empty;
            Console.WriteLine($"  {l.Position}  {l.Name}{marker}");
        }
}, workspaceOpt, jsonOpt);

// ── lane add ──────────────────────────────────────────────────────────────────

var laneAddNameArg = new Argument<string>("name", "Lane name");

var laneAddCmd = new Command("add", "Add a lane to a workspace");
laneAddCmd.AddArgument(laneAddNameArg);
laneAddCmd.AddOption(workspaceOpt);
laneAddCmd.SetHandler(async (string name, string? workspace) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lane = await mediator.Send(new AddLaneCommand(ws.Id, name));
    Console.WriteLine($"Added lane '{lane.Name}' at position {lane.Position} in workspace '{ws.Name}'");
}, laneAddNameArg, workspaceOpt);

// ── lane rename ───────────────────────────────────────────────────────────────

var laneRenameNameArg = new Argument<string>("lane-name", "Current lane name");
var laneNewNameOpt = new Option<string>("--new-name", "New lane name") { IsRequired = true };

var laneRenameCmd = new Command("rename", "Rename a lane");
laneRenameCmd.AddArgument(laneRenameNameArg);
laneRenameCmd.AddOption(laneNewNameOpt);
laneRenameCmd.AddOption(workspaceOpt);
laneRenameCmd.SetHandler(async (string laneName, string newName, string? workspace) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var target = lanes.FirstOrDefault(l => string.Equals(l.Name, laneName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{laneName}' not found in workspace '{ws.Name}'.");
    var renamed = await mediator.Send(new RenameLaneCommand(target.Id, newName));
    Console.WriteLine($"Renamed lane '{laneName}' → '{renamed.Name}' in workspace '{ws.Name}'");
}, laneRenameNameArg, laneNewNameOpt, workspaceOpt);

// ── lane move ─────────────────────────────────────────────────────────────────

var laneMoveNameArg = new Argument<string>("lane-name", "Lane name");
var laneToPositionOpt = new Option<int>("--to-position", "Target position (1-based)") { IsRequired = true };

var laneMoveCmd = new Command("move", "Move a lane to a different position");
laneMoveCmd.AddArgument(laneMoveNameArg);
laneMoveCmd.AddOption(laneToPositionOpt);
laneMoveCmd.AddOption(workspaceOpt);
laneMoveCmd.SetHandler(async (string laneName, int toPosition, string? workspace) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var target = lanes.FirstOrDefault(l => string.Equals(l.Name, laneName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{laneName}' not found in workspace '{ws.Name}'.");
    var moved = await mediator.Send(new MoveLaneCommand(target.Id, toPosition));
    Console.WriteLine($"Moved lane '{moved.Name}' to position {moved.Position} in workspace '{ws.Name}'");
}, laneMoveNameArg, laneToPositionOpt, workspaceOpt);

// ── lane remove ───────────────────────────────────────────────────────────────

var laneRemoveNameArg = new Argument<string>("lane-name", "Lane name");

var laneRemoveCmd = new Command("remove", "Remove an empty lane");
laneRemoveCmd.AddArgument(laneRemoveNameArg);
laneRemoveCmd.AddOption(workspaceOpt);
laneRemoveCmd.SetHandler(async (string laneName, string? workspace) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    var target = lanes.FirstOrDefault(l => string.Equals(l.Name, laneName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Lane '{laneName}' not found in workspace '{ws.Name}'.");
    await mediator.Send(new RemoveLaneCommand(target.Id));
    Console.WriteLine($"Removed lane '{laneName}' from workspace '{ws.Name}'");
}, laneRemoveNameArg, workspaceOpt);

// ── wire lane command ─────────────────────────────────────────────────────────

var laneCmd = new Command("lane", "Manage kanban lanes");
laneCmd.AddCommand(laneListCmd);
laneCmd.AddCommand(laneAddCmd);
laneCmd.AddCommand(laneRenameCmd);
laneCmd.AddCommand(laneMoveCmd);
laneCmd.AddCommand(laneRemoveCmd);
root.AddCommand(laneCmd);

// ── install-skills ────────────────────────────────────────────────────────────

var installSkillsCmd = new Command(
    "install-skills",
    "Copy bundled skills to ~/.claude/skills/ (overwrites existing).");
installSkillsCmd.SetHandler(() =>
{
    var sourceDir = Path.Combine(AppContext.BaseDirectory, "skills");
    if (!Directory.Exists(sourceDir))
    {
        Console.Error.WriteLine($"No skills/ directory bundled with bishop (expected at {sourceDir}).");
        Environment.ExitCode = 1;
        return;
    }

    var destRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        "skills");
    Directory.CreateDirectory(destRoot);

    var installed = 0;
    foreach (var skillSourceDir in Directory.GetDirectories(sourceDir))
    {
        var name = Path.GetFileName(skillSourceDir);
        var sourceFiles = Directory.GetFiles(skillSourceDir, "*", SearchOption.AllDirectories);
        if (sourceFiles.Length == 0)
        {
            // Empty husk left in bin/ output by MSBuild after a skill rename — content-copy
            // semantics don't delete files removed from source. Skip silently rather than
            // print a misleading "Installed" line for a directory with nothing in it.
            continue;
        }

        var skillDestDir = Path.Combine(destRoot, name);
        foreach (var file in sourceFiles)
        {
            var relative = Path.GetRelativePath(skillSourceDir, file);
            var destFile = Path.Combine(skillDestDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
        Console.WriteLine($"Installed skill '{name}' to {skillDestDir}");
        installed++;
    }

    if (installed == 0)
    {
        Console.WriteLine("No skills found to install.");
    }
});
root.AddCommand(installSkillsCmd);

// ── work-next ─────────────────────────────────────────────────────────────────

var workNextTagOpt = new Option<string?>("--tag", () => null, "Only claim cards carrying this tag (omit for any tag)");
var workNextMaxOpt = new Option<int>("--max", () => 10, "Max cards to process; 0 means uncapped");
var workNextModelOpt = new Option<string?>("--model", () => null, "Claude model ID to pass to claude (omit to use claude's default)");

var workNextCmd = new Command("work-next", "Loop: claim a tagged card and run claude on it until exhaustion, failure, or cap");
workNextCmd.AddOption(workspaceOpt);
workNextCmd.AddOption(workNextTagOpt);
workNextCmd.AddOption(workNextMaxOpt);
workNextCmd.AddOption(workNextModelOpt);
workNextCmd.SetHandler(async (string? workspace, string? tag, int max, string? model) =>
{
    if (max < 0)
    {
        Console.Error.WriteLine("--max must be >= 0 (0 means uncapped).");
        Environment.ExitCode = 1;
        return;
    }

    var ws = await resolver.ResolveAsync(workspace);
    var result = await mediator.Send(new WorkNextCommand(ws.Id, ws.Path, tag, max, model));

    var stamp = DateTimeOffset.Now.ToString("HH:mm:ss");
    var summary = $"[{stamp}] Processed {result.CardsProcessed} card(s). Stopped: {result.StopReason}";
    if (result.FailedCardNumber is { } failed)
        summary += $" on card #{failed}";
    Console.Out.WriteLine(summary + ".");

    switch (result.StopReason)
    {
        case WorkNextStopReason.EmptyLane:
        case WorkNextStopReason.CapReached:
        case WorkNextStopReason.Cancelled:
            break;
        case WorkNextStopReason.DirtyWorkingTree:
            Console.Error.WriteLine($"Working tree at '{ws.Path}' is dirty:");
            foreach (var path in result.DirtyPaths ?? Array.Empty<string>())
                Console.Error.WriteLine($"  {path}");
            Environment.ExitCode = 1;
            break;
        case WorkNextStopReason.ClaudeFailed:
            Console.Error.WriteLine($"Card #{result.FailedCardNumber} left in 'Doing'.");
            Environment.ExitCode = 1;
            break;
        case WorkNextStopReason.NotAGitRepo:
            Console.Error.WriteLine($"Workspace '{ws.Path}' is not a git repository.");
            Environment.ExitCode = 1;
            break;
        case WorkNextStopReason.GitNotFound:
            Console.Error.WriteLine("'git' executable not found on PATH.");
            Environment.ExitCode = 1;
            break;
    }
}, workspaceOpt, workNextTagOpt, workNextMaxOpt, workNextModelOpt);
root.AddCommand(workNextCmd);

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

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class Program { }

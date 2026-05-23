using Bishop.App;
using Bishop.Core;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Cards.PushLane;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Skills.GetSkillBootstrapInfo;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Cli.Cards.Add;
using Bishop.Cli.Cards.Edit;
using Bishop.Cli.Cards.List;
using Bishop.Cli.Cards.Remove;
using Bishop.Cli.Cards.View;
using Bishop.Cli.Workspaces.Current;
using Bishop.Cli.Workspaces.Init;
using Bishop.Cli.Workspaces.List;
using Bishop.Cli.Workspaces.SetGitHub;
using Bishop.Cli.Workspaces.UnsetGitHub;
using Bishop.App.WorkNext;
using Bishop.Cli;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
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
var cardResolver = new CardResolver(mediator);

var root = new RootCommand("Bishop AI — kanban CLI");

// ── workspace ───────────────────────────────────────────────────────────────

var workspaceCmd = new Command("workspace", "Manage workspaces");
workspaceCmd.AddCommand(new ListWorkspacesCliCommand(mediator));
workspaceCmd.AddCommand(new CurrentWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new InitWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new SetGitHubCliCommand(mediator));
workspaceCmd.AddCommand(new UnsetGitHubCliCommand(mediator));
root.AddCommand(workspaceCmd);

// ── card move ─────────────────────────────────────────────────────────────────

var cardIdArg = new Argument<string>("card-id", "Card short ID or prefix");
var toLaneOpt = new Option<string>("--to-lane", "Target lane name") { IsRequired = true };
var toPositionOpt = new Option<int>("--to-position", "Target zero-based position") { IsRequired = true };
var noCloseOpt = new Option<bool>("--no-close", "Skip auto-close when moving into the Done lane");

var cardMoveCmd = new Command("move", "Move a card to another lane or position");
cardMoveCmd.AddArgument(cardIdArg);
cardMoveCmd.AddOption(CommonOptions.WorkspaceOption);
cardMoveCmd.AddOption(toLaneOpt);
cardMoveCmd.AddOption(toPositionOpt);
cardMoveCmd.AddOption(noCloseOpt);
cardMoveCmd.SetHandler(async (string prefix, string? workspace, string toLane, int toPosition, bool noClose) =>
{
    var resolved = await cardResolver.ResolveAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, _, _) = resolved.Value;
    var card = await mediator.Send(new MoveCardCommand(cardId, toLane, toPosition, noClose));
    Console.WriteLine($"Moved card #{card.Number} → [{card.LaneName}] position {card.Position}");
}, cardIdArg, CommonOptions.WorkspaceOption, toLaneOpt, toPositionOpt, noCloseOpt);

// ── card claim ────────────────────────────────────────────────────────────────

var claimSourceLaneOpt = new Option<string>("--lane", () => SystemLaneNames.ToDo, "Source lane to claim from");
var claimTagOpt = new Option<string?>("--tag", "Only claim the first card carrying this tag");

var cardClaimCmd = new Command("claim", "Pick the top card from a lane and move it to Doing");
cardClaimCmd.AddOption(CommonOptions.WorkspaceOption);
cardClaimCmd.AddOption(claimSourceLaneOpt);
cardClaimCmd.AddOption(claimTagOpt);
cardClaimCmd.AddOption(CommonOptions.JsonOption);
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
            laneName = card.LaneName,
            position = card.Position,
            createdAt = card.CreatedAt,
            updatedAt = card.UpdatedAt,
            tag = card.TagName
        }, jsonOpts));
    }
    else
    {
        Console.WriteLine($"Claimed #{card.Number} — '{card.Title}' [{sourceLaneName}] → [{card.LaneName}]");
        if (card.TagName is not null)
            Console.WriteLine($"Tag: {card.TagName}");
        if (!string.IsNullOrEmpty(card.Description))
        {
            Console.WriteLine();
            Console.WriteLine(card.Description);
        }
    }
}, CommonOptions.WorkspaceOption, claimSourceLaneOpt, claimTagOpt, CommonOptions.JsonOption);

// ── tag list ──────────────────────────────────────────────────────────────────

var tagListCmd = new Command("list", "List tags in a workspace");
tagListCmd.AddOption(CommonOptions.WorkspaceOption);
tagListCmd.AddOption(CommonOptions.JsonOption);
tagListCmd.SetHandler(async (string? workspace, bool json) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var tags = await mediator.Send(new ListTagsByWorkspaceQuery(ws.Id));
    if (json)
        Console.WriteLine(JsonSerializer.Serialize(tags, jsonOpts));
    else
        foreach (var t in tags)
            Console.WriteLine(t.Name);
}, CommonOptions.WorkspaceOption, CommonOptions.JsonOption);

// ── wire tag command ──────────────────────────────────────────────────────────

var tagCmd = new Command("tag", "Manage workspace tags");
tagCmd.AddCommand(tagListCmd);
root.AddCommand(tagCmd);

// ── card push ─────────────────────────────────────────────────────────────────

var cardPushIdArg = new Argument<string?>("card-id", "Card short ID or prefix (mutually exclusive with --lane)")
{
    Arity = ArgumentArity.ZeroOrOne
};
var cardPushLaneOpt = new Option<string?>("--lane", "Push all unlinked cards in a lane (mutually exclusive with card-id)");
var cardPushDryRunOpt = new Option<bool>("--dry-run", "Preview what would be pushed without calling gh");

var cardPushCmd = new Command("push", "Push a card to GitHub Issues");
cardPushCmd.AddArgument(cardPushIdArg);
cardPushCmd.AddOption(cardPushLaneOpt);
cardPushCmd.AddOption(cardPushDryRunOpt);
cardPushCmd.AddOption(CommonOptions.WorkspaceOption);
cardPushCmd.SetHandler(async (string? prefix, string? lane, bool dryRun, string? workspace) =>
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

// ── card import-from-github ───────────────────────────────────────────────────

var importLabelOpt = new Option<string?>("--label", "Filter to issues carrying this GitHub label");
var importLimitOpt = new Option<int>("--limit", () => 100, "Maximum number of issues to import");
var importDryRunOpt = new Option<bool>("--dry-run", "Preview what would be imported without writing anything");

var cardImportFromGitHubCmd = new Command("import-from-github", "Import open GitHub issues as cards in the To Do lane");
cardImportFromGitHubCmd.AddOption(importLabelOpt);
cardImportFromGitHubCmd.AddOption(importLimitOpt);
cardImportFromGitHubCmd.AddOption(importDryRunOpt);
cardImportFromGitHubCmd.AddOption(CommonOptions.JsonOption);
cardImportFromGitHubCmd.AddOption(CommonOptions.WorkspaceOption);
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
}, importLabelOpt, importLimitOpt, importDryRunOpt, CommonOptions.JsonOption, CommonOptions.WorkspaceOption);

// ── card close ────────────────────────────────────────────────────────────────

var cardCloseIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardCloseCmd = new Command("close", "Mark a card as closed");
cardCloseCmd.AddArgument(cardCloseIdArg);
cardCloseCmd.AddOption(CommonOptions.WorkspaceOption);
cardCloseCmd.SetHandler(async (string prefix, string? workspace) =>
{
    var resolved = await cardResolver.ResolveAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, cardNumber, _) = resolved.Value;
    await mediator.Send(new CloseCardCommand(cardId));
    Console.WriteLine($"Closed card #{cardNumber}");
}, cardCloseIdArg, CommonOptions.WorkspaceOption);

// ── card reopen ───────────────────────────────────────────────────────────────

var cardReopenIdArg = new Argument<string>("card-id", "Card short ID or prefix");

var cardReopenCmd = new Command("reopen", "Reopen a closed card");
cardReopenCmd.AddArgument(cardReopenIdArg);
cardReopenCmd.AddOption(CommonOptions.WorkspaceOption);
cardReopenCmd.SetHandler(async (string prefix, string? workspace) =>
{
    var resolved = await cardResolver.ResolveAsync(workspace, prefix);
    if (resolved is null) return;
    var (cardId, cardNumber, _) = resolved.Value;
    await mediator.Send(new ReopenCardCommand(cardId));
    Console.WriteLine($"Reopened card #{cardNumber}");
}, cardReopenIdArg, CommonOptions.WorkspaceOption);

// ── wire card command ─────────────────────────────────────────────────────────

var cardCmd = new Command("card", "Manage kanban cards");
cardCmd.AddCommand(new AddCardCliCommand(mediator));
cardCmd.AddCommand(new ViewCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(cardMoveCmd);
cardCmd.AddCommand(new RemoveCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new EditCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(cardClaimCmd);
cardCmd.AddCommand(new ListCardsCliCommand(mediator));
cardCmd.AddCommand(cardPushCmd);
cardCmd.AddCommand(cardImportFromGitHubCmd);
cardCmd.AddCommand(cardCloseCmd);
cardCmd.AddCommand(cardReopenCmd);
root.AddCommand(cardCmd);

// ── lane list ─────────────────────────────────────────────────────────────────

var laneListCmd = new Command("list", "List lanes in a workspace");
laneListCmd.AddOption(CommonOptions.WorkspaceOption);
laneListCmd.AddOption(CommonOptions.JsonOption);
laneListCmd.SetHandler(async (string? workspace, bool json) =>
{
    var ws = await resolver.ResolveAsync(workspace);
    var lanes = await mediator.Send(new ListLanesByWorkspaceQuery(ws.Id));
    if (json)
        Console.WriteLine(JsonSerializer.Serialize(lanes, jsonOpts));
    else
        foreach (var l in lanes)
            Console.WriteLine($"  {l.Position}  {l.Name}");
}, CommonOptions.WorkspaceOption, CommonOptions.JsonOption);

// ── wire lane command ─────────────────────────────────────────────────────────

var laneCmd = new Command("lane", "Inspect kanban lanes");
laneCmd.AddCommand(laneListCmd);
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

// ── skill bootstrap ───────────────────────────────────────────────────────────

var skillBootstrapCmd = new Command(
    "bootstrap",
    "Emit workspace + tag/lane info for a skill preamble. Non-zero exit if not in a workspace.");
skillBootstrapCmd.AddOption(CommonOptions.JsonOption);
skillBootstrapCmd.SetHandler(async (InvocationContext context) =>
{
    var json = context.ParseResult.GetValueForOption(CommonOptions.JsonOption);

    Workspace ws;
    try
    {
        ws = await resolver.ResolveAsync(null);
    }
    catch (InvalidOperationException)
    {
        Console.Error.WriteLine(
            "Not in a Bishop workspace. Run `bishop workspace list` to see available workspaces, then `cd` into one of the listed paths and retry.");
        context.ExitCode = 1;
        return;
    }

    var info = await mediator.Send(new GetSkillBootstrapInfoQuery(ws.Id));

    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            workspaceName = info.WorkspaceName,
            workspacePath = info.WorkspacePath,
            gitHubRepo = info.GitHubRepo,
            tags = info.Tags.Select(t => new { name = t.Name, colour = t.Colour }),
            lanes = info.Lanes.Select(l => new { name = l.Name, position = l.Position })
        }, jsonOpts));
    }
    else
    {
        Console.WriteLine($"Workspace: {info.WorkspaceName}");
        Console.WriteLine($"Path:      {info.WorkspacePath}");
        if (!string.IsNullOrEmpty(info.GitHubRepo))
            Console.WriteLine($"GitHub:    {info.GitHubRepo}");
        Console.WriteLine($"Tags:      {string.Join(", ", info.Tags.Select(t => t.Name))}");
        Console.WriteLine($"Lanes:     {string.Join(", ", info.Lanes.OrderBy(l => l.Position).Select(l => l.Name))}");
    }
});

var skillCmd = new Command("skill", "Skill runtime utilities");
skillCmd.AddCommand(skillBootstrapCmd);
root.AddCommand(skillCmd);

// ── work-next ─────────────────────────────────────────────────────────────────

var workNextTagOpt = new Option<string?>("--tag", () => null, "Only claim cards carrying this tag (omit for any tag)");
var workNextMaxOpt = new Option<int>("--max", () => 10, "Max cards to process; 0 means uncapped");
var workNextModelOpt = new Option<string?>("--model", () => null, "Claude model ID to pass to claude (omit to use claude's default)");

var workNextCmd = new Command("work-next", "Loop: claim a tagged card and run claude on it until exhaustion, failure, or cap");
workNextCmd.AddOption(CommonOptions.WorkspaceOption);
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
}, CommonOptions.WorkspaceOption, workNextTagOpt, workNextMaxOpt, workNextModelOpt);
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

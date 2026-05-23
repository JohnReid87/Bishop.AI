using Bishop.App;
using Bishop.Core;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Skills.GetSkillBootstrapInfo;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Cli.Cards.Add;
using Bishop.Cli.Cards.Claim;
using Bishop.Cli.Cards.Close;
using Bishop.Cli.Cards.Edit;
using Bishop.Cli.Cards.ImportFromGitHub;
using Bishop.Cli.Cards.List;
using Bishop.Cli.Cards.Move;
using Bishop.Cli.Cards.Push;
using Bishop.Cli.Cards.Remove;
using Bishop.Cli.Cards.Reopen;
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

// ── wire card command ─────────────────────────────────────────────────────────

var cardCmd = new Command("card", "Manage kanban cards");
cardCmd.AddCommand(new AddCardCliCommand(mediator));
cardCmd.AddCommand(new ViewCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new MoveCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new RemoveCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new EditCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new ClaimCardCliCommand(mediator));
cardCmd.AddCommand(new ListCardsCliCommand(mediator));
cardCmd.AddCommand(new PushCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new ImportFromGitHubCliCommand(mediator));
cardCmd.AddCommand(new CloseCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new ReopenCardCliCommand(mediator, cardResolver));
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

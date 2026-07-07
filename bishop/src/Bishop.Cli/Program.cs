using Bishop.App;
using Bishop.Cli;
using Bishop.Cli.Batches.Abandon;
using Bishop.Cli.Batches.AddCard;
using Bishop.Cli.Batches.CleanUp;
using Bishop.Cli.Batches.Create;
using Bishop.Cli.Batches.Merge;
using Bishop.Cli.Batches.Edit;
using Bishop.Cli.Batches.List;
using Bishop.Cli.Batches.Prune;
using Bishop.Cli.Batches.Remove;
using Bishop.Cli.Batches.RemoveCard;
using Bishop.Cli.Batches.Rescue;
using Bishop.Cli.Batches.Run;
using Bishop.Cli.Batches.Salvage;
using Bishop.Cli.Batches.Show;
using Bishop.Cli.Cards.Create;
using Bishop.Cli.Cards.Claim;
using Bishop.Cli.Cards.Close;
using Bishop.Cli.Cards.Edit;
using Bishop.Cli.Cards.List;
using Bishop.Cli.Cards.Move;
using Bishop.Cli.Cards.Remove;
using Bishop.Cli.Cards.Reopen;
using Bishop.Cli.Cards.SetCommit;
using Bishop.Cli.Cards.Show;
using Bishop.Cli.Cards.Star;
using Bishop.Cli.Cards.Unstar;
using Bishop.Cli.Findings.Record;
using Bishop.App.Context.ContextPack;
using Bishop.Cli.Context.Pack;
using Bishop.Cli.Context.Print;
using Bishop.Cli.InstallSkills;
using Bishop.Cli.Lanes.List;
using Bishop.Cli.Life.Auth;
using Bishop.Cli.Life.Speak;
using Bishop.Cli.Bootstrap;
using Bishop.Cli.Hooks.CheckPath;
using Bishop.Cli.Hooks.SpeakOnStop;
using Bishop.Cli.Tags.List;
using Bishop.Cli.Workspaces.Current;
using Bishop.Cli.Workspaces.Init;
using Bishop.Cli.Workspaces.List;
using Bishop.Cli.Workspaces.Purge;
using Bishop.Cli.Workspaces.Remove;
using Bishop.Cli.Workspaces.RecordSkillRun;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

Console.InputEncoding = new System.Text.UTF8Encoding(false);
Console.OutputEncoding = new System.Text.UTF8Encoding(false);

var builder = Host.CreateEmptyApplicationBuilder(null);
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddBishopApp(BishopDbConnectionString.Resolve());
using var host = builder.Build();

await host.StartAsync();

var mediator = host.Services.GetRequiredService<ISender>();
var timeProvider = host.Services.GetRequiredService<TimeProvider>();
var cardResolver = new CardResolver(mediator);

var root = new RootCommand("Bishop AI — kanban CLI");

// ── workspace ───────────────────────────────────────────────────────────────

var workspaceCmd = new Command("workspace", "Manage workspaces");
workspaceCmd.AddCommand(new ListWorkspacesCliCommand(mediator));
workspaceCmd.AddCommand(new CurrentWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new InitWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new RemoveWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new PurgeWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new RecordSkillRunCliCommand(mediator));
root.AddCommand(workspaceCmd);


// ── tag ───────────────────────────────────────────────────────────────────────

var tagCmd = new Command("tag", "Manage workspace tags");
tagCmd.AddCommand(new ListTagsCliCommand(mediator));
root.AddCommand(tagCmd);

// ── wire card command ─────────────────────────────────────────────────────────

var cardCmd = new Command("card", "Manage kanban cards");
cardCmd.AddCommand(new CreateCardCliCommand(mediator));
cardCmd.AddCommand(new ShowCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new MoveCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new RemoveCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new EditCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new ClaimCardCliCommand(mediator));
cardCmd.AddCommand(new ListCardsCliCommand(mediator));
cardCmd.AddCommand(new CloseCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new ReopenCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new StarCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new UnstarCardCliCommand(mediator, cardResolver));
cardCmd.AddCommand(new SetCommitCardCliCommand(mediator, cardResolver));
root.AddCommand(cardCmd);

// ── batch ─────────────────────────────────────────────────────────────────────

var batchCmd = new Command("batch", "Manage batches");
batchCmd.AddCommand(new CreateBatchCliCommand(mediator));
batchCmd.AddCommand(new EditBatchCliCommand(mediator));
batchCmd.AddCommand(new ListBatchesCliCommand(mediator));
batchCmd.AddCommand(new ShowBatchCliCommand(mediator));
batchCmd.AddCommand(new AddCardToBatchCliCommand(mediator, cardResolver));
batchCmd.AddCommand(new RemoveCardFromBatchCliCommand(mediator, cardResolver));
batchCmd.AddCommand(new RunBatchCliCommand(mediator, timeProvider));
batchCmd.AddCommand(new RescueBatchCliCommand(mediator));
batchCmd.AddCommand(new SalvageBatchCliCommand(mediator));
batchCmd.AddCommand(new MergeBatchCliCommand(mediator));
batchCmd.AddCommand(new CleanUpBatchCliCommand(mediator));
batchCmd.AddCommand(new AbandonBatchCliCommand(mediator));
batchCmd.AddCommand(new RemoveBatchCliCommand(mediator));
batchCmd.AddCommand(new PruneBatchCliCommand(mediator, timeProvider));
root.AddCommand(batchCmd);

// ── lane ──────────────────────────────────────────────────────────────────────

var laneCmd = new Command("lane", "Inspect kanban lanes");
laneCmd.AddCommand(new ListLanesCliCommand(mediator));
root.AddCommand(laneCmd);

// ── findings ──────────────────────────────────────────────────────────────────

var findingsCmd = new Command("findings", "Record review-skill findings");
findingsCmd.AddCommand(new RecordFindingsCliCommand(mediator));
root.AddCommand(findingsCmd);

// ── install-skills ────────────────────────────────────────────────────────────

root.AddCommand(new InstallSkillsCliCommand());

// ── skill ─────────────────────────────────────────────────────────────────────

var skillCmd = new Command("skill", "Skill runtime utilities");
skillCmd.AddCommand(new BootstrapSkillCliCommand(mediator));
root.AddCommand(skillCmd);

// ── context ───────────────────────────────────────────────────────────────────

var contextCmd = new Command("context", "Inspect the workspace context file");
contextCmd.AddCommand(new PrintContextCliCommand(mediator));
root.AddCommand(contextCmd);

var contextProviders = host.Services.GetServices<IContextProvider>();
var contextPackCmd = new PrintContextPackCliCommand(mediator, contextProviders);
contextPackCmd.AddCommand(new LifeStandupContextPackCliCommand(new Bishop.Life.Core.LifePlanFileService(), timeProvider));
root.AddCommand(contextPackCmd);

// ── life ──────────────────────────────────────────────────────────────────────

var lifeCmd = new Command("life", "bishop.life subcommands");
if (OperatingSystem.IsWindows())
{
    var lifeAuthCmd = new Command("auth", "Authorize external integrations for bishop.life");
    lifeAuthCmd.AddCommand(new AuthGoogleCliCommand());
    lifeCmd.AddCommand(lifeAuthCmd);
    lifeCmd.AddCommand(new SpeakCliCommand());
    lifeCmd.AddCommand(new SpeakPreludeCliCommand());
}
root.AddCommand(lifeCmd);

// ── hook ──────────────────────────────────────────────────────────────────────

var hookCmd = new Command("hook", "Claude Code hook utilities");
hookCmd.AddCommand(new CheckPathCliCommand(timeProvider));
if (OperatingSystem.IsWindows())
{
    hookCmd.AddCommand(new SpeakOnStopCliCommand());
}
root.AddCommand(hookCmd);

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

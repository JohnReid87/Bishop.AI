using Bishop.App;
using Bishop.Cli;
using Bishop.Cli.Batches.Abandon;
using Bishop.Cli.Batches.AddCard;
using Bishop.Cli.Batches.Create;
using Bishop.Cli.Batches.Finish;
using Bishop.Cli.Batches.List;
using Bishop.Cli.Batches.Prune;
using Bishop.Cli.Batches.RemoveCard;
using Bishop.Cli.Batches.Run;
using Bishop.Cli.Batches.View;
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
using Bishop.Cli.Cards.SetCommit;
using Bishop.Cli.Cards.View;
using Bishop.Cli.Context.Print;
using Bishop.Cli.InstallSkills;
using Bishop.Cli.Lanes.List;
using Bishop.Cli.Bootstrap;
using Bishop.Cli.Tags.List;
using Bishop.Cli.WorkNext;
using Bishop.Cli.Workspaces.Current;
using Bishop.Cli.Workspaces.Init;
using Bishop.Cli.Workspaces.List;
using Bishop.Cli.Workspaces.Purge;
using Bishop.Cli.Workspaces.Remove;
using Bishop.Cli.Workspaces.SetGitHub;
using Bishop.Cli.Workspaces.RecordSkillRun;
using Bishop.Cli.Workspaces.UnsetGitHub;
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
builder.Services.AddBishopApp(BishopDbConnectionString.Resolve(), BishopStampPath.Resolve());
using var host = builder.Build();

await host.StartAsync();

var mediator = host.Services.GetRequiredService<ISender>();
var cardResolver = new CardResolver(mediator);

var root = new RootCommand("Bishop AI — kanban CLI");

// ── workspace ───────────────────────────────────────────────────────────────

var workspaceCmd = new Command("workspace", "Manage workspaces");
workspaceCmd.AddCommand(new ListWorkspacesCliCommand(mediator));
workspaceCmd.AddCommand(new CurrentWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new InitWorkspaceCliCommand(mediator));
workspaceCmd.AddCommand(new SetGitHubCliCommand(mediator));
workspaceCmd.AddCommand(new UnsetGitHubCliCommand(mediator));
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
cardCmd.AddCommand(new SetCommitCardCliCommand(mediator, cardResolver));
root.AddCommand(cardCmd);

// ── batch ─────────────────────────────────────────────────────────────────────

var batchCmd = new Command("batch", "Manage batches");
batchCmd.AddCommand(new CreateBatchCliCommand(mediator));
batchCmd.AddCommand(new ListBatchesCliCommand(mediator));
batchCmd.AddCommand(new ViewBatchCliCommand(mediator));
batchCmd.AddCommand(new AddCardToBatchCliCommand(mediator, cardResolver));
batchCmd.AddCommand(new RemoveCardFromBatchCliCommand(mediator, cardResolver));
batchCmd.AddCommand(new RunBatchCliCommand(mediator));
batchCmd.AddCommand(new FinishBatchCliCommand(mediator));
batchCmd.AddCommand(new AbandonBatchCliCommand(mediator));
batchCmd.AddCommand(new PruneBatchCliCommand(mediator));
root.AddCommand(batchCmd);

// ── lane ──────────────────────────────────────────────────────────────────────

var laneCmd = new Command("lane", "Inspect kanban lanes");
laneCmd.AddCommand(new ListLanesCliCommand(mediator));
root.AddCommand(laneCmd);

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

// ── work-next ─────────────────────────────────────────────────────────────────

root.AddCommand(new WorkNextCliCommand(mediator));

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

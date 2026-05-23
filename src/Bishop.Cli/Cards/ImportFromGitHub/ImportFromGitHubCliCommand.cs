using Bishop.App.Cards.ImportFromGitHub;
using MediatR;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Cards.ImportFromGitHub;

internal sealed class ImportFromGitHubCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ImportFromGitHubCliCommand(IMediator mediator) : base("import-from-github", "Import open GitHub issues as cards in the To Do lane")
    {
        var resolver = new WorkspaceResolver(mediator);
        var importLabelOpt = new Option<string?>("--label", "Filter to issues carrying this GitHub label");
        var importLimitOpt = new Option<int>("--limit", () => 100, "Maximum number of issues to import");
        var importDryRunOpt = new Option<bool>("--dry-run", "Preview what would be imported without writing anything");

        AddOption(importLabelOpt);
        AddOption(importLimitOpt);
        AddOption(importDryRunOpt);
        AddOption(CommonOptions.JsonOption);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string? label, int limit, bool dryRun, bool json, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            var result = await mediator.Send(new ImportFromGitHubCommand(ws.Id, label, limit, dryRun));
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result, s_jsonOpts));
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
    }
}

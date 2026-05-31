using Bishop.App.Findings.RecordFindings;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Findings.Record;

internal sealed class RecordFindingsCliCommand : Command
{
    public RecordFindingsCliCommand(ISender mediator)
        : base("record", "Record review-skill findings (persists to DB; writes compat HTML under .bishop/findings/)")
    {
        var resolver = new WorkspaceResolver(mediator);
        var skillOption = new Option<string>("--skill", "Name of the skill recording findings") { IsRequired = true };
        var fileOption = new Option<string>("--file", "Path to findings JSON file (use - for stdin)") { IsRequired = true };
        var shaOption = new Option<string>("--sha", "Git SHA at the time the skill ran") { IsRequired = true };

        AddOption(skillOption);
        AddOption(fileOption);
        AddOption(shaOption);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string skill, string file, string sha, string? workspace) =>
        {
            var json = file == "-"
                ? await Console.In.ReadToEndAsync()
                : await File.ReadAllTextAsync(file);

            var ws = await resolver.ResolveAsync(workspace);
            var result = await mediator.Send(new RecordFindingsCommand(ws.Id, ws.Path, skill, json, sha));

            Console.WriteLine($"Recorded {result.FindingCount} finding{(result.FindingCount == 1 ? "" : "s")} for '{skill}'");
            Console.WriteLine($"  html: {result.HtmlPath}");
        }, skillOption, fileOption, shaOption, CommonOptions.WorkspaceOption);
    }
}

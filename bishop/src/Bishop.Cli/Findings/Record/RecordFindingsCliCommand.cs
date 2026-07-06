using Bishop.App.Batches.GetBatch;
using Bishop.App.Findings.RecordFindings;
using MediatR;
using System.CommandLine;
using System.Text.Json.Nodes;

namespace Bishop.Cli.Findings.Record;

internal sealed class RecordFindingsCliCommand : Command
{
    public RecordFindingsCliCommand(ISender mediator)
        : base("record", "Record review-skill findings (persists to DB; view in the Findings page).")
    {
        var resolver = new WorkspaceResolver(mediator);
        var skillOption = new Option<string>("--skill", "Name of the skill recording findings") { IsRequired = true };
        var fileOption = new Option<string>("--file", "Path to findings JSON file (use - for stdin)") { IsRequired = true };
        var shaOption = new Option<string>("--sha", "Git SHA at the time the skill ran") { IsRequired = true };
        var projectOption = new Option<string?>("--project", "Project name scoping this run (e.g. 'Bishop.App'); overrides any 'projectName' in the JSON payload");
        var batchOption = new Option<string?>("--batch", "Batch name scoping this run; findings are keyed to the batch so per-batch rebuttal/dedup memory works across re-reviews");

        AddOption(skillOption);
        AddOption(fileOption);
        AddOption(shaOption);
        AddOption(projectOption);
        AddOption(batchOption);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string skill, string file, string sha, string? project, string? batch, string? workspace) =>
        {
            var json = file == "-"
                ? await Console.In.ReadToEndAsync()
                : await File.ReadAllTextAsync(file);

            if (!string.IsNullOrWhiteSpace(project))
                json = InjectProjectName(json, project);

            var ws = await resolver.ResolveAsync(workspace);

            Guid? batchId = null;
            if (!string.IsNullOrWhiteSpace(batch))
                batchId = (await mediator.Send(new GetBatchQuery(batch))).Batch.Id;

            var result = await mediator.Send(new RecordFindingsCommand(ws.Id, ws.Path, skill, json, sha, batchId));

            Console.WriteLine($"Recorded {result.FindingCount} finding{(result.FindingCount == 1 ? "" : "s")} for '{skill}'");
        }, skillOption, fileOption, shaOption, projectOption, batchOption, CommonOptions.WorkspaceOption);
    }

    private static string InjectProjectName(string json, string project)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
            throw new InvalidOperationException("Findings JSON root must be an object when --project is supplied.");
        root["projectName"] = project;
        return root.ToJsonString();
    }
}

using Bishop.App.Workspaces.RecordSkillRun;
using MediatR;
using System.CommandLine;

namespace Bishop.Cli.Workspaces.RecordSkillRun;

internal sealed class RecordSkillRunCliCommand : Command
{
    public RecordSkillRunCliCommand(ISender mediator)
        : base("record-skill-run", "Record that a review skill ran on the current workspace")
    {
        var resolver = new WorkspaceResolver(mediator);
        var skillOption = new Option<string>("--skill", "Name of the skill that ran") { IsRequired = true };
        var shaOption = new Option<string>("--sha", "Git SHA at the time the skill ran") { IsRequired = true };

        AddOption(skillOption);
        AddOption(shaOption);
        AddOption(CommonOptions.WorkspaceOption);

        this.SetHandler(async (string skill, string sha, string? workspace) =>
        {
            var ws = await resolver.ResolveAsync(workspace);
            await mediator.Send(new RecordSkillRunCommand(ws.Id, skill, sha));
            Console.WriteLine($"Recorded '{skill}' run on workspace '{ws.Name}' at {sha}");
        }, skillOption, shaOption, CommonOptions.WorkspaceOption);
    }
}

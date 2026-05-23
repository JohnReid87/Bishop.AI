using Bishop.App.Skills.GetSkillBootstrapInfo;
using MediatR;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Cli.Bootstrap;

internal sealed class BootstrapSkillCliCommand : Command
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public BootstrapSkillCliCommand(IMediator mediator)
        : base("bootstrap", "Emit workspace + tag/lane info for a skill preamble. Non-zero exit if not in a workspace.")
    {
        var resolver = new WorkspaceResolver(mediator);

        AddOption(CommonOptions.JsonOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var json = context.ParseResult.GetValueForOption(CommonOptions.JsonOption);

            Bishop.Core.Workspace ws;
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
                }, s_jsonOpts));
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
    }
}

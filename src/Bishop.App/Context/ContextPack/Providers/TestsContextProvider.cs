using Bishop.App.Findings.GetPriorFindings;
using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

public sealed class TestsContextProvider : IContextProvider
{
    public string SkillName => "tests";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Card model",
        "Findings Recording Procedure"
    };

    public async Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var prior = await mediator.Send(
            new GetPriorFindingsQuery(workspace.Id, "bish-tests"), cancellationToken);
        return new { PriorFindings = prior };
    }
}

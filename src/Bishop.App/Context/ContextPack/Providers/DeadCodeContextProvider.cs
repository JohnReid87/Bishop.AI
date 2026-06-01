using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

internal sealed class DeadCodeContextProvider : IContextProvider
{
    public string SkillName => "dead-code";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Card model",
        "Findings Recording Procedure"
    };

    public Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);
}

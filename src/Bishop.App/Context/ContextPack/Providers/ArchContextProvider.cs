using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

public sealed class ArchContextProvider : IContextProvider
{
    public string SkillName => "arch";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Card model",
        "Skill-Run Recording Procedure"
    };

    public Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);
}

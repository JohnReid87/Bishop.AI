using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

public sealed class GrillDocsContextProvider : IContextProvider
{
    public string SkillName => "grill-docs";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection"
    };

    public Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);
}

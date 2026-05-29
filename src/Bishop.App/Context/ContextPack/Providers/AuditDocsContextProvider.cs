using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack.Providers;

public sealed class AuditDocsContextProvider : IContextProvider
{
    public string SkillName => "audit-docs";

    public IReadOnlyList<string> RequiredSections { get; } = new[]
    {
        "Shell selection",
        "Findings Recording Procedure"
    };

    public Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);
}

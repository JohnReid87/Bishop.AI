using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack;

public interface IContextProvider
{
    string SkillName { get; }

    IReadOnlyList<string> RequiredSections { get; }

    Task<object?> BuildSkillSpecificAsync(
        ContextPackArgs args,
        Workspace workspace,
        ISender mediator,
        CancellationToken cancellationToken);
}

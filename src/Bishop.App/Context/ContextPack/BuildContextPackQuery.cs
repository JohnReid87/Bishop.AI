using Bishop.Core;
using MediatR;

namespace Bishop.App.Context.ContextPack;

public sealed record BuildContextPackQuery(
    string SkillName,
    Workspace Workspace,
    ContextPackArgs Args) : IRequest<ContextPack>;

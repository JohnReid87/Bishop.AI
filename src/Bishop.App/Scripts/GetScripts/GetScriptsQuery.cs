using MediatR;

namespace Bishop.App.Scripts.GetScripts;

public sealed record GetScriptsQuery : IRequest<IReadOnlyList<ScriptInfo>>;

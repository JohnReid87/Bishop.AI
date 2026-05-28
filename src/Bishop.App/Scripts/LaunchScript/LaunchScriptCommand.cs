using MediatR;

namespace Bishop.App.Scripts.LaunchScript;

public sealed record LaunchScriptCommand(string ScriptPath, string Args = "") : IRequest<bool>;

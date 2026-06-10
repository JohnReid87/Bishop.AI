using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Workspaces.LaunchPlainTerminal;

public sealed record LaunchPlainTerminalCommand(string Path, TerminalSnap? Snap = null) : IRequest<bool>;

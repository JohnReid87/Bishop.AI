using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Batches.LaunchBatchTerminal;

public sealed record LaunchBatchTerminalCommand(
    string WorkspacePath,
    string BatchName,
    string Model,
    bool Resume,
    TerminalSnap? Snap = null) : IRequest<bool>;

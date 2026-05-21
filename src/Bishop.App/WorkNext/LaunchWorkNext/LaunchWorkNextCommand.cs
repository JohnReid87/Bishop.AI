using Bishop.App.Terminal;
using MediatR;

namespace Bishop.App.WorkNext.LaunchWorkNext;

// Tag is null for "Any" (omits the --tag flag). Max of 0 passes through as `--max 0` (uncapped).
public sealed record LaunchWorkNextCommand(
    string WorkspacePath,
    string? Tag,
    int Max,
    TerminalSnap? Snap = null,
    string Model = "claude-sonnet-4-6") : IRequest<bool>;

namespace Bishop.Life.Core;

/// <summary>
/// Centralises the claude-CLI argument strings the Life host hands to the
/// terminal launcher and PTY launcher. Stand-up runs headless inside a PTY,
/// so it forces <c>--permission-mode bypassPermissions</c> — any unhandled
/// permission prompt would stall the session invisibly (card #1085). The
/// init/add launches run in a visible Windows Terminal, where prompts work
/// normally, so they intentionally omit the bypass.
/// </summary>
public static class LifeClaudeArgs
{
    public static string Standup(string sessionId) =>
        $"/bish-life-standup --session-id {sessionId} --permission-mode bypassPermissions";

    public static string Init() => "/bish-life-init";

    public static string Add() => "/bish-life-add";
}

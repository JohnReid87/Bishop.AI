namespace Bishop.Core;

/// <summary>
/// The truthful, derived lifecycle state shown for a batch. Distinct from the persisted
/// <see cref="BatchStatus"/> (Open / Working / Closed): <c>Finished</c> and <c>Merged</c> are
/// computed from timestamps and member-card progress so the UI stops reporting <c>Working</c>
/// forever after a run completes or <c>Open</c> forever after a hand-worked delivery lands.
/// </summary>
public enum BatchDisplayState
{
    Open,
    Working,
    Finished,
    Merged,
    Closed
}

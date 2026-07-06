namespace Bishop.Core;

/// <summary>
/// Derives the truthful <see cref="BatchDisplayState"/> from a batch's persisted status,
/// its lifecycle timestamps, and whether every member card has reached Done. Shared by the
/// Batches table and the board group header so both surfaces tell the same story.
/// </summary>
public static class BatchDisplayStates
{
    /// <param name="allCardsDone">
    /// True only when the batch has at least one member card and every one of them is in the
    /// Done lane. A batch delivered by hand (never transitioned to Working) still reads as
    /// Finished once this holds, which is why the rule is not gated on <see cref="BatchStatus.Working"/>.
    /// </param>
    public static BatchDisplayState Derive(
        BatchStatus status,
        DateTimeOffset? finishedAt,
        DateTimeOffset? mergedAt,
        bool allCardsDone)
    {
        if (status == BatchStatus.Closed)
            return BatchDisplayState.Closed;
        if (mergedAt is not null)
            return BatchDisplayState.Merged;
        if (finishedAt is not null || allCardsDone)
            return BatchDisplayState.Finished;
        if (status == BatchStatus.Working)
            return BatchDisplayState.Working;
        return BatchDisplayState.Open;
    }
}

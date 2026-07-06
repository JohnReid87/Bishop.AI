using Bishop.Core;
using Bishop.ViewModels.Batches;

namespace Bishop.ViewModels.Workspaces;

internal static class BoardBatchStats
{
    private record struct Accumulator(
        string Name,
        int TotalCount,
        int DoneCount,
        DateTimeOffset? CreatedAt,
        BatchStatus Status,
        DateTimeOffset? FinishedAt,
        DateTimeOffset? MergedAt,
        DateTimeOffset? StoppedAt);

    public static IReadOnlyDictionary<Guid, BatchStats> Compute(IEnumerable<LaneViewModel> lanes)
    {
        var raw = AccumulatePerBatch(lanes);
        var indexByBatch = AssignAccentIndices(raw);
        return raw.ToDictionary(
            kvp => kvp.Key,
            kvp => new BatchStats(
                kvp.Value.Name,
                kvp.Value.TotalCount,
                kvp.Value.DoneCount,
                indexByBatch[kvp.Key],
                kvp.Value.Status,
                kvp.Value.FinishedAt,
                kvp.Value.MergedAt,
                kvp.Value.StoppedAt));
    }

    private static Dictionary<Guid, Accumulator> AccumulatePerBatch(IEnumerable<LaneViewModel> lanes)
    {
        var raw = new Dictionary<Guid, Accumulator>();
        foreach (var lane in lanes)
            foreach (var card in lane.Cards)
                AccumulateCard(raw, card);
        return raw;
    }

    private static void AccumulateCard(Dictionary<Guid, Accumulator> raw, Bishop.ViewModels.Cards.CardViewModel card)
    {
        if (card.BatchId is not { } batchId) return;
        raw.TryGetValue(batchId, out var e);
        raw[batchId] = new Accumulator(
            ResolveName(e.Name, card.BatchName),
            e.TotalCount + 1,
            e.DoneCount + (card.LaneName == SystemLaneNames.Done ? 1 : 0),
            e.CreatedAt ?? card.BatchCreatedAt,
            // Batch-level fields are constant across a batch's cards; the first card that carries
            // them wins and later cards leave them unchanged.
            card.BatchStatus ?? e.Status,
            e.FinishedAt ?? card.BatchFinishedAt,
            e.MergedAt ?? card.BatchMergedAt,
            e.StoppedAt ?? card.BatchStoppedAt);
    }

    private static string ResolveName(string? existing, string? fromCard)
        => existing is not (null or "") ? existing : fromCard ?? string.Empty;

    private static Dictionary<Guid, int> AssignAccentIndices(Dictionary<Guid, Accumulator> raw)
    {
        return raw
            .OrderBy(kvp => kvp.Value.CreatedAt ?? DateTimeOffset.MaxValue)
            .Select((kvp, i) => (kvp.Key, Index: i))
            .ToDictionary(x => x.Key, x => x.Index);
    }
}

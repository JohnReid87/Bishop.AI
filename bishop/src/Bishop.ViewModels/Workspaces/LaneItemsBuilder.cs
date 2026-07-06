using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

internal static class LaneItemsBuilder
{
    // Reconciles an ObservableCollection in-place to match wanted, preserving existing
    // references to avoid the Reset notification that Clear() emits.
    internal static void ReconcileItems<T>(ObservableCollection<T> collection, IList<T> wanted) where T : class
    {
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < collection.Count)
            {
                if (!ReferenceEquals(collection[i], wanted[i]))
                    collection[i] = wanted[i];
            }
            else
                collection.Add(wanted[i]);
        }
        while (collection.Count > wanted.Count)
            collection.RemoveAt(collection.Count - 1);
    }

    internal static (Dictionary<Guid, BatchGroupViewModel> ActiveGroups, Dictionary<Guid, List<CardViewModel>> GroupCards, List<object> Target)
        Build(
            IReadOnlyList<CardViewModel> filteredCards,
            IReadOnlyDictionary<Guid, BatchStats> batchStats,
            Dictionary<Guid, BatchGroupViewModel> existingGroups)
    {
        var activeGroups = new Dictionary<Guid, BatchGroupViewModel>();
        var groupCards = new Dictionary<Guid, List<CardViewModel>>();
        var target = new List<object>(filteredCards.Count);

        foreach (var card in filteredCards)
        {
            if (card.BatchId is { } batchId)
            {
                if (!activeGroups.TryGetValue(batchId, out var group))
                {
                    group = GetOrCreateGroup(batchId, batchStats, existingGroups, card);
                    activeGroups[batchId] = group;
                    groupCards[batchId] = [];
                    target.Add(group);
                }
                groupCards[batchId].Add(card);
            }
            else
                target.Add(card);
        }

        return (activeGroups, groupCards, target);
    }

    private static BatchGroupViewModel GetOrCreateGroup(
        Guid batchId,
        IReadOnlyDictionary<Guid, BatchStats> batchStats,
        Dictionary<Guid, BatchGroupViewModel> existingGroups,
        CardViewModel card)
    {
        if (!existingGroups.TryGetValue(batchId, out var group))
            group = new BatchGroupViewModel { BatchId = batchId };

        if (batchStats.TryGetValue(batchId, out var s))
            (group.BatchName, group.TotalCount, group.DoneCount, group.AccentIndex, group.Status, group.FinishedAt, group.MergedAt)
                = (s.Name, s.TotalCount, s.DoneCount, s.AccentIndex, s.Status, s.FinishedAt, s.MergedAt);
        else
            group.BatchName = card.BatchName ?? string.Empty;

        return group;
    }
}

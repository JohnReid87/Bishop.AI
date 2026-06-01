using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTags;
using Bishop.Core;
using Bishop.ViewModels.Shared;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

internal static class BoardRefresher
{
    public sealed record Context(
        ISender Mediator,
        Guid WorkspaceId,
        ObservableCollection<LaneViewModel> Lanes,
        Func<string, LaneViewModel> CreateLaneVm,
        bool IsCardSkillsButtonVisible,
        string SearchText,
        IUiDispatcher? UiDispatcher = null);

    public static async Task RefreshAsync(Context ctx)
    {
        var lanes = await ctx.Mediator.Send(new ListLanesByWorkspaceQuery(ctx.WorkspaceId));
        var tags = await ctx.Mediator.Send(new ListTagsQuery());
        var tagColourByName = tags.ToDictionary(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);

        var cardsByLane = await LoadCardsByLaneAsync(ctx);

        await RunOnUiThreadAsync(ctx.UiDispatcher, () =>
        {
            if (CanUpdateInPlace(ctx.Lanes, lanes))
            {
                UpdateLanesInPlace(ctx, cardsByLane, tagColourByName);
            }
            else
            {
                RebuildLanes(ctx, lanes, cardsByLane, tagColourByName);
            }

            var batchStats = BoardBatchStats.Compute(ctx.Lanes);
            foreach (var laneVm in ctx.Lanes)
                laneVm.RebuildLaneItems(batchStats);
        });
    }

    // Marshals the synchronous mutation block onto the UI thread when a dispatcher is supplied.
    // ObservableCollection<T> raises COMException (HRESULT 0x8001010E) if mutated off the UI thread under WinUI.
    private static Task RunOnUiThreadAsync(IUiDispatcher? dispatcher, Action work)
    {
        if (dispatcher is null)
        {
            work();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.TryEnqueue(() =>
        {
            try
            {
                work();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static bool CanUpdateInPlace(
        IReadOnlyCollection<LaneViewModel> current,
        IReadOnlyCollection<LaneInfo> fresh)
    {
        return current.Count == fresh.Count
            && current.Select(l => l.Name).SequenceEqual(fresh.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<Card>>> LoadCardsByLaneAsync(Context ctx)
    {
        var all = await ctx.Mediator.Send(new ListCardsByWorkspaceQuery(ctx.WorkspaceId));
        var grouped = new Dictionary<string, IReadOnlyList<Card>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in all.GroupBy(c => c.LaneName, StringComparer.OrdinalIgnoreCase))
            grouped[group.Key] = group.ToList();
        return grouped;
    }

    private static IReadOnlyList<Card> GetLaneCards(IReadOnlyDictionary<string, IReadOnlyList<Card>> cardsByLane, string laneName)
        => cardsByLane.TryGetValue(laneName, out var cards) ? cards : Array.Empty<Card>();

    private static void UpdateLanesInPlace(
        Context ctx,
        IReadOnlyDictionary<string, IReadOnlyList<Card>> cardsByLane,
        IReadOnlyDictionary<string, string> tagColourByName)
    {
        foreach (var laneVm in ctx.Lanes)
        {
            var fresh = GetLaneCards(cardsByLane, laneVm.Name);
            ReconcileLaneCards(laneVm, fresh, tagColourByName, ctx.IsCardSkillsButtonVisible);
        }
    }

    private static void ReconcileLaneCards(
        LaneViewModel laneVm,
        IReadOnlyList<Bishop.Core.Card> fresh,
        IReadOnlyDictionary<string, string> tagColourByName,
        bool isCardSkillsButtonVisible)
    {
        for (var i = 0; i < fresh.Count; i++)
        {
            var card = fresh[i];
            if (i < laneVm.Cards.Count && BoardCardFactory.Matches(laneVm.Cards[i], card, tagColourByName))
                continue;
            var cardVm = BoardCardFactory.Build(card, laneVm.Name, tagColourByName, isCardSkillsButtonVisible);
            if (i < laneVm.Cards.Count)
                laneVm.Cards[i] = cardVm;
            else
                laneVm.Cards.Add(cardVm);
        }
        while (laneVm.Cards.Count > fresh.Count)
            laneVm.Cards.RemoveAt(laneVm.Cards.Count - 1);
    }

    private static void RebuildLanes(
        Context ctx,
        IReadOnlyList<LaneInfo> lanes,
        IReadOnlyDictionary<string, IReadOnlyList<Card>> cardsByLane,
        IReadOnlyDictionary<string, string> tagColourByName)
    {
        ctx.Lanes.Clear();
        foreach (var lane in lanes)
        {
            var laneVm = ctx.CreateLaneVm(lane.Name);
            foreach (var card in GetLaneCards(cardsByLane, lane.Name))
                laneVm.Cards.Add(BoardCardFactory.Build(card, lane.Name, tagColourByName, ctx.IsCardSkillsButtonVisible));
            ctx.Lanes.Add(laneVm);
        }

        if (!string.IsNullOrEmpty(ctx.SearchText))
        {
            foreach (var laneVm in ctx.Lanes)
                laneVm.ApplyFilter(ctx.SearchText);
        }
    }
}

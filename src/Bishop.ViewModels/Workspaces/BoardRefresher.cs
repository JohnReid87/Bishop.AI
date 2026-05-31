using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTags;
using Bishop.Core;
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
        string SearchText);

    public static async Task RefreshAsync(Context ctx)
    {
        var lanes = await ctx.Mediator.Send(new ListLanesByWorkspaceQuery(ctx.WorkspaceId));
        var tags = await ctx.Mediator.Send(new ListTagsQuery());
        var tagColourByName = tags.ToDictionary(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);

        if (CanUpdateInPlace(ctx.Lanes, lanes))
        {
            await UpdateLanesInPlaceAsync(ctx, tagColourByName);
        }
        else
        {
            await RebuildLanesAsync(ctx, lanes, tagColourByName);
        }

        var batchStats = BoardBatchStats.Compute(ctx.Lanes);
        foreach (var laneVm in ctx.Lanes)
            laneVm.RebuildLaneItems(batchStats);
    }

    private static bool CanUpdateInPlace(
        IReadOnlyCollection<LaneViewModel> current,
        IReadOnlyCollection<LaneInfo> fresh)
    {
        return current.Count == fresh.Count
            && current.Select(l => l.Name).SequenceEqual(fresh.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task UpdateLanesInPlaceAsync(Context ctx, IReadOnlyDictionary<string, string> tagColourByName)
    {
        foreach (var laneVm in ctx.Lanes)
        {
            var fresh = (await ctx.Mediator.Send(new ListCardsByWorkspaceQuery(ctx.WorkspaceId, LaneName: laneVm.Name))).ToList();
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

    private static async Task RebuildLanesAsync(
        Context ctx,
        IReadOnlyList<LaneInfo> lanes,
        IReadOnlyDictionary<string, string> tagColourByName)
    {
        ctx.Lanes.Clear();
        foreach (var lane in lanes)
        {
            var laneVm = ctx.CreateLaneVm(lane.Name);
            var cards = await ctx.Mediator.Send(new ListCardsByWorkspaceQuery(ctx.WorkspaceId, LaneName: lane.Name));
            foreach (var card in cards)
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

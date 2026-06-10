using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;

namespace Bishop.ViewModels.Workspaces;

internal static class BoardSelection
{
    public static void Toggle(CardViewModel card, BatchStagingTrayViewModel tray)
    {
        card.IsSelected = !card.IsSelected;
        if (card.IsSelected)
            AddIfMissing(tray, card);
        else
            tray.Cards.Remove(card);
    }

    public static void Clear(IEnumerable<LaneViewModel> lanes, BatchStagingTrayViewModel tray)
    {
        foreach (var lane in lanes)
            foreach (var c in lane.Cards)
                c.IsSelected = false;
        tray.Reset();
    }

    private static void AddIfMissing(BatchStagingTrayViewModel tray, CardViewModel card)
    {
        if (!tray.Cards.Contains(card))
            tray.Cards.Add(card);
    }
}

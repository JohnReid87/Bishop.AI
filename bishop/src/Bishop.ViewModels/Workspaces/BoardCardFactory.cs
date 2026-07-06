using Bishop.ViewModels.Cards;

namespace Bishop.ViewModels.Workspaces;

internal static class BoardCardFactory
{
    public static CardViewModel Build(
        Bishop.Core.Card card,
        string laneName,
        IReadOnlyDictionary<string, string> tagColourByName,
        bool isSkillsButtonVisible)
    {
        return new CardViewModel
        {
            Id = card.Id,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = laneName,
            TagName = card.TagName,
            TagColour = ResolveTagColour(card.TagName, tagColourByName),
            IsClosed = card.IsClosed,
            LastAutoRunFailedAt = card.LastAutoRunFailedAt,
            LastAutoRunSucceededAt = card.LastAutoRunSucceededAt,
            BatchId = card.BatchId,
            BatchName = card.Batch?.Name,
            BatchCreatedAt = card.Batch?.CreatedAt,
            BatchStatus = card.Batch?.Status,
            BatchFinishedAt = card.Batch?.FinishedAt,
            BatchMergedAt = card.Batch?.MergedAt,
            IsSkillsButtonVisible = isSkillsButtonVisible,
        };
    }

    public static bool Matches(
        CardViewModel vm,
        Bishop.Core.Card card,
        IReadOnlyDictionary<string, string> tagColourByName)
    {
        return ScalarFieldsMatch(vm, card)
            && TagMatches(vm, card, tagColourByName);
    }

    private static bool ScalarFieldsMatch(CardViewModel vm, Bishop.Core.Card card)
    {
        return (vm.Id, vm.Title, vm.Description, vm.IsClosed) ==
                   (card.Id, card.Title, card.Description, card.IsClosed)
               && (vm.LastAutoRunFailedAt, vm.LastAutoRunSucceededAt, vm.BatchId) ==
                   (card.LastAutoRunFailedAt, card.LastAutoRunSucceededAt, card.BatchId);
    }

    private static bool TagMatches(
        CardViewModel vm,
        Bishop.Core.Card card,
        IReadOnlyDictionary<string, string> tagColourByName)
    {
        var expectedColour = ResolveTagColour(card.TagName, tagColourByName);
        return vm.TagName == card.TagName && vm.TagColour == expectedColour;
    }

    private static string? ResolveTagColour(
        string? tagName,
        IReadOnlyDictionary<string, string> tagColourByName)
    {
        return tagName is { } name && tagColourByName.TryGetValue(name, out var c) ? c : null;
    }
}

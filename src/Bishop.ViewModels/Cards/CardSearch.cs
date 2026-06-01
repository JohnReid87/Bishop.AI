namespace Bishop.ViewModels.Cards;

internal static class CardSearch
{
    internal static bool Matches(string title, string? tagName, int number, string description, string searchText)
    {
        var query = searchText.StartsWith('#') ? searchText[1..] : searchText;
        return title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (tagName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
               number.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

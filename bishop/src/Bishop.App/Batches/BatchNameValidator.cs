using System.Text.RegularExpressions;

namespace Bishop.App.Batches;

internal static partial class BatchNameValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9\-_ ]+$")]
    private static partial Regex AllowedChars();

    internal static void Validate(string trimmedName)
    {
        if (!AllowedChars().IsMatch(trimmedName))
            throw new ArgumentException(
                "Batch name may only contain letters, digits, spaces, hyphens, and underscores.");
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bishop.App.Findings;

internal static partial class FindingsValidator
{
    [GeneratedRegex(@"^carded:#\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex CardedOutcomeRegex();

    public static FindingsDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FindingsValidationException("findings JSON is empty.");

        using var doc = ParseDocument(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new FindingsValidationException("findings JSON root must be an object.");

        if (!root.TryGetProperty("findings", out var findingsEl))
            throw new FindingsValidationException("findings JSON must contain a 'findings' array.");

        if (findingsEl.ValueKind != JsonValueKind.Array)
            throw new FindingsValidationException("'findings' must be an array.");

        return new FindingsDocument(ParseFindingsList(findingsEl), ParseProjectName(root));
    }

    private static string? ParseProjectName(JsonElement root)
    {
        if (!root.TryGetProperty("projectName", out var projectEl) || projectEl.ValueKind == JsonValueKind.Null)
            return null;

        if (projectEl.ValueKind != JsonValueKind.String)
            throw new FindingsValidationException("'projectName' must be a string when present.");

        var value = projectEl.GetString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static List<Finding> ParseFindingsList(JsonElement findingsEl)
    {
        var findings = new List<Finding>(findingsEl.GetArrayLength());
        var index = 0;
        foreach (var item in findingsEl.EnumerateArray())
        {
            findings.Add(ParseFinding(item, index));
            index++;
        }
        return findings;
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FindingsValidationException($"findings JSON is malformed: {ex.Message}", ex);
        }
    }

    private static Finding ParseFinding(JsonElement el, int index)
    {
        if (el.ValueKind != JsonValueKind.Object)
            throw new FindingsValidationException($"findings[{index}] must be an object.");

        var title = RequiredString(el, "title", index);
        var body = RequiredString(el, "body", index);
        var outcome = RequiredString(el, "outcome", index);

        ValidateOutcome(outcome, index);

        return new Finding(
            title, body, outcome,
            OptionalString(el, "severity", index),
            OptionalString(el, "location", index),
            OptionalString(el, "file", index),
            OptionalString(el, "rule", index),
            OptionalString(el, "symbol", index));
    }

    private static void ValidateOutcome(string outcome, int index)
    {
        if (outcome is not "dismissed" and not "parked" && !CardedOutcomeRegex().IsMatch(outcome))
            throw new FindingsValidationException(
                $"findings[{index}].outcome must be 'dismissed', 'parked', or 'carded:#<n>'; got '{outcome}'.");
    }

    private static string RequiredString(JsonElement el, string name, int index)
    {
        if (!el.TryGetProperty(name, out var prop))
            throw new FindingsValidationException($"findings[{index}].{name} is required.");
        if (prop.ValueKind != JsonValueKind.String)
            throw new FindingsValidationException($"findings[{index}].{name} must be a string.");
        var value = prop.GetString();
        if (string.IsNullOrEmpty(value))
            throw new FindingsValidationException($"findings[{index}].{name} must not be empty.");
        return value;
    }

    private static string? OptionalString(JsonElement el, string name, int index)
    {
        if (!el.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Null) return null;
        if (prop.ValueKind != JsonValueKind.String)
            throw new FindingsValidationException($"findings[{index}].{name} must be a string when present.");
        var value = prop.GetString();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

public sealed class FindingsValidationException : Exception
{
    public FindingsValidationException(string message) : base(message) { }
    public FindingsValidationException(string message, Exception inner) : base(message, inner) { }
}

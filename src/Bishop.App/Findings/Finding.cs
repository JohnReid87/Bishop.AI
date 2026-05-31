namespace Bishop.App.Findings;

public sealed record Finding(
    string Title,
    string Body,
    string Outcome,
    string? Severity = null,
    string? Location = null,
    string? File = null,
    string? Rule = null,
    string? Symbol = null);

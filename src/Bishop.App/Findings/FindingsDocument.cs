namespace Bishop.App.Findings;

internal sealed record FindingsDocument(IReadOnlyList<Finding> Findings, string? ProjectName = null);

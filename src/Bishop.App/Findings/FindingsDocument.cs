namespace Bishop.App.Findings;

public sealed record FindingsDocument(IReadOnlyList<Finding> Findings, string? ProjectName = null);

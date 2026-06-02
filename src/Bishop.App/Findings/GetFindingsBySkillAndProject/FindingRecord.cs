namespace Bishop.App.Findings.GetFindingsBySkillAndProject;

internal sealed record FindingRecord(
    Guid Id,
    string Title,
    string Body,
    string? Severity,
    string? File,
    string? Symbol,
    string? Rule,
    string Status,
    string? RebuttalText,
    int? LinkedCardId);

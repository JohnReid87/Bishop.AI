namespace Bishop.App.Findings.GetFindingsBySkillAndProject;

public sealed record FindingRecord(
    Guid Id,
    string Title,
    string Body,
    string? Severity,
    string? File,
    string? Symbol,
    string? Rule,
    string Status,
    int? LinkedCardId);

namespace Bishop.App.Findings.GetPriorFindings;

internal sealed record PriorFindingRecord(
    string IdentityHash,
    string? ProjectName,
    string? File,
    string? Symbol,
    string? Rule,
    string Title,
    string Status,
    string? RebuttalText,
    int? LinkedCardNumber);

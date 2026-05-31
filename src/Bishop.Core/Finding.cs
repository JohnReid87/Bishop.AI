namespace Bishop.Core;

public sealed class Finding
{
    public Guid Id { get; set; }
    public Guid WorkspaceSkillRunId { get; set; }
    public string IdentityHash { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? ProjectName { get; set; }
    public string? File { get; set; }
    public string? Symbol { get; set; }
    public string? Rule { get; set; }
    public string? Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? RebuttalText { get; set; }
    public int? LinkedCardId { get; set; }

    public WorkspaceSkillRun Run { get; set; } = null!;
}

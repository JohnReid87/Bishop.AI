namespace Bishop.Core;

public sealed class WorkspaceSkillRun
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public string GitSha { get; set; } = string.Empty;
    public int FindingsCount { get; set; }

    public Workspace Workspace { get; set; } = null!;
}

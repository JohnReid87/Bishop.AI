using System.Text.Json.Serialization;

namespace Bishop.Core;

public sealed class Batch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public BatchStatus Status { get; set; }
    public BatchClosedReason? ClosedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string WorktreePath { get; set; } = string.Empty;
    public string? GitHubPrUrl { get; set; }

    [JsonIgnore]
    public ICollection<Card> Cards { get; set; } = [];
}

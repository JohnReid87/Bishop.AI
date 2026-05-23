using System.Text.Json.Serialization;

namespace Bishop.Core;

public sealed class Card : IAuditable
{
    public Guid Id { get; set; }
    public Guid LaneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Number { get; set; }
    public int Position { get; set; }
    public bool IsClosed { get; set; }
    public int? GitHubIssueNumber { get; set; }
    public DateTimeOffset? GitHubPushedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int ClaudeRunCount { get; set; }

    public Guid? TagId { get; set; }

    [JsonIgnore]
    public Lane Lane { get; set; } = null!;
    [JsonIgnore]
    public Tag? Tag { get; set; }
}

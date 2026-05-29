using System.Text.Json.Serialization;

namespace Bishop.Core;

public sealed class Batch
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public BatchStatus Status { get; set; }
    public BatchClosedReason? ClosedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string WorktreePath { get; set; } = string.Empty;
    public DateTimeOffset? StoppedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    [JsonIgnore]
    public ICollection<Card> Cards { get; set; } = [];

    public void TransitionToWorking()
    {
        if (Status != BatchStatus.Open)
            throw new InvalidOperationException(
                $"Batch must be Open to transition to Working; current status is {Status}.");
        Status = BatchStatus.Working;
    }

    public void Close(BatchClosedReason reason, DateTimeOffset now)
    {
        if (Status == BatchStatus.Closed)
            throw new InvalidOperationException($"Batch {Id} is already Closed.");
        Status = BatchStatus.Closed;
        ClosedReason = reason;
        ClosedAt = now;
    }
}

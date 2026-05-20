using System.Text.Json.Serialization;

namespace Bishop.Core;

public sealed class Lane
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsSystem { get; set; }

    [JsonIgnore]
    public Workspace Workspace { get; set; } = null!;
    public ICollection<Card> Cards { get; set; } = [];
}

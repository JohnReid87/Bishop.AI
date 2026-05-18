namespace Bishop.Core;

public sealed class Tag
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Colour { get; set; } = "#888888";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<CardTag> CardTags { get; set; } = [];
}

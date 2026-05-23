namespace Bishop.Core;

public sealed class Workspace : IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Position { get; set; }
    public int NextCardNumber { get; set; } = 1;
    public string? GitHubRepo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Lane> Lanes { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<Card> Cards { get; set; } = [];
}

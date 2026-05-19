namespace Bishop.Core;

public sealed class Card
{
    public Guid Id { get; set; }
    public Guid LaneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Number { get; set; }
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Lane Lane { get; set; } = null!;
    public ICollection<CardTag> CardTags { get; set; } = [];
}

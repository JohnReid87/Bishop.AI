namespace Bishop.Core;

public sealed class Workspace : IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Position { get; set; }
    public int NextCardNumber { get; set; } = 1;
    public bool IsRemoved { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset? HiddenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Card> Cards { get; set; } = [];

    public Workspace With(string? path = null) => new()
    {
        Id = Id,
        Name = Name,
        Path = path ?? Path,
        Position = Position,
        NextCardNumber = NextCardNumber,
        IsRemoved = IsRemoved,
        RemovedAt = RemovedAt,
        IsHidden = IsHidden,
        HiddenAt = HiddenAt,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Cards = Cards,
    };
}

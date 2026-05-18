namespace Bishop.Core;

public sealed class CardTag
{
    public Guid CardId { get; set; }
    public Guid TagId { get; set; }

    public Card Card { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

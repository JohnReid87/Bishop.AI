namespace Bishop.Core;

public sealed class FxRate
{
    public Guid WorkspaceId { get; set; }
    public decimal UsdToGbp { get; set; }
    public DateTimeOffset FetchedAtUtc { get; set; }
}

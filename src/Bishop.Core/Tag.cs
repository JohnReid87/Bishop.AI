using System.Text.Json.Serialization;

namespace Bishop.Core;

public sealed class Tag
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Colour { get; set; } = BrandTagPalette.DefaultColour;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonIgnore]
    public Workspace Workspace { get; set; } = null!;
    [JsonIgnore]
    public ICollection<CardTag> CardTags { get; set; } = [];
}

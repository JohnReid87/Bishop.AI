using System.Text.Json;
using System.Text.Json.Serialization;
using Bishop.Core;

namespace Bishop.App.WorkNext;

public sealed record AutoCardPromptContext(
    SkillBootstrapInfo Bootstrap,
    AutoCardPromptCard Card,
    IReadOnlyList<AutoCardCommitSummary> RecentCommits,
    IReadOnlyList<AutoCardPromptCard> RelatedCards)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string FormatAsBlock()
    {
        var payload = new
        {
            workspace = new
            {
                name = Bootstrap.WorkspaceName,
                path = Bootstrap.WorkspacePath,
                gitHubRepo = Bootstrap.GitHubRepo,
                lanes = Bootstrap.Lanes.Select(l => l.Name).ToList(),
                tags = Bootstrap.Tags.Select(t => t.Name).ToList(),
            },
            card = Card,
            git = new { recentCommits = RecentCommits },
            relatedCards = RelatedCards,
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return $"<bishop-context>\n{json}\n</bishop-context>";
    }
}

public sealed record AutoCardPromptCard(
    int Number,
    string Title,
    string Description,
    string LaneName,
    string? Tag,
    bool IsClosed);

public sealed record AutoCardCommitSummary(string ShortHash, string Subject);

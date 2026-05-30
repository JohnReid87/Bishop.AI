using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bishop.App.Cards.AddCard;
using Bishop.App.Git;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.Docs;

/// <summary>
/// Sibling of <see cref="SkillContextPackSchemaTests"/> covering the camelCase
/// JSON surface emitted by <c>bishop card show --json</c>. The context-pack
/// test only walks snake_case paths rooted at <c>workspace|git|skill_specific|conventions</c>,
/// so camelCase tokens documented for <c>card show --json</c> (e.g. <c>laneName</c>,
/// <c>gitHubIssueNumber</c>) are not validated by it. See card #774.
/// </summary>
public sealed class SkillCardViewSchemaTests : IClassFixture<DbFixture>
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SkillsRoot = Path.Combine(RepoRoot, "skills");

    // ShowCardCliCommand uses the default System.Text.Json options — no naming
    // policy override, so anonymous-type property names land verbatim (camelCase).
    private static readonly JsonSerializerOptions s_camelCaseOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    // Backticked camelCase identifier, optionally dotted (e.g. `commit.shortHash`).
    // Lower-case first char excludes things like regex literals, command words.
    private static readonly Regex s_tokenRegex = new(
        @"`(?<path>[a-z][a-zA-Z0-9]*(?:\.[a-z][a-zA-Z0-9]*)*)`",
        RegexOptions.Compiled);

    private readonly IDbContextFactory<BishopDbContext> _factory;

    public SkillCardViewSchemaTests(DbFixture fixture) => _factory = fixture.Factory;

    [Theory]
    [InlineData("bish-write-skill")]
    public async Task SkillMd_DocumentedCardViewJsonFields_ExistInSerializedPayload(string skillDirName)
    {
        var skillMdPath = Path.Combine(SkillsRoot, skillDirName, "SKILL.md");
        File.Exists(skillMdPath).Should().BeTrue($"expected SKILL.md at {skillMdPath}");

        var skillBody = File.ReadAllText(skillMdPath);
        var documentedTokens = ExtractTokens(skillBody);
        documentedTokens.Should().NotBeEmpty(
            $"{skillDirName}/SKILL.md mentions `bishop card show --json` and should " +
            "reference at least one camelCase field from its output");

        var payloadJson = SerializeSampleCardViewPayload(await BuildSampleCardAsync());
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var failures = new List<string>();
        foreach (var token in documentedTokens)
        {
            var segments = token.Split('.');
            if (!TryWalk(root, segments, out var failedAt, out var availableKeys))
            {
                var closest = ClosestKey(failedAt, availableKeys);
                var hint = closest is null
                    ? $"available keys at that level: {string.Join(", ", availableKeys)}"
                    : $"did you mean '{closest}'? (available: {string.Join(", ", availableKeys)})";
                failures.Add($"  `{token}` — missing segment '{failedAt}'; {hint}");
            }
        }

        failures.Should().BeEmpty(
            $"every backticked camelCase token documented near `bishop card show --json` in " +
            $"skills/{skillDirName}/SKILL.md must resolve in the serialized payload — " +
            $"ShowCardCliCommand emits an anonymous type with no PropertyNamingPolicy, so the " +
            $"property names land verbatim. Drift means agents capture the wrong field. " +
            $"Mismatches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    /// <summary>
    /// Extracts backticked camelCase tokens that appear in any blank-line-delimited
    /// paragraph mentioning <c>bishop card show ... --json</c>. Scoping to that
    /// paragraph keeps unrelated camelCase mentions (other CLI commands, code
    /// references) out of the assertion set.
    /// </summary>
    private static IReadOnlyList<string> ExtractTokens(string skillBody)
    {
        // Normalize line endings then split on blank lines.
        var normalized = skillBody.Replace("\r\n", "\n").Replace("\r", "\n");
        var paragraphs = Regex.Split(normalized, @"\n\s*\n");

        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var paragraph in paragraphs)
        {
            if (!paragraph.Contains("bishop card show", StringComparison.Ordinal)) continue;
            if (!paragraph.Contains("--json", StringComparison.Ordinal)) continue;
            foreach (Match m in s_tokenRegex.Matches(paragraph))
            {
                tokens.Add(m.Groups["path"].Value);
            }
        }
        return tokens.ToList();
    }

    /// <summary>
    /// Mirrors the anonymous-type shape emitted by
    /// <see cref="Bishop.Cli.Cards.Show.ShowCardCliCommand"/> — kept in sync by
    /// reading the same fields off the same <see cref="Card"/> type. If
    /// ShowCardCliCommand's shape changes, update both sides; this test guards
    /// the doc against the shape, not the shape itself.
    /// </summary>
    private static string SerializeSampleCardViewPayload(Card card)
    {
        var commit = new CommitInfo("abc1234", "abc1234567890abcdef", "feat: seed", "", DateTimeOffset.UtcNow, false);
        var gitHubRepo = "Owner/Repo";

        var payload = new
        {
            id = card.Id,
            number = card.Number,
            title = card.Title,
            description = card.Description,
            laneName = card.LaneName,
            position = card.Position,
            isClosed = card.IsClosed,
            gitHubIssueNumber = card.GitHubIssueNumber,
            gitHubPushedAt = card.GitHubPushedAt,
            createdAt = card.CreatedAt,
            updatedAt = card.UpdatedAt,
            totalInputTokens = card.TotalInputTokens,
            totalOutputTokens = card.TotalOutputTokens,
            claudeRunCount = card.ClaudeRunCount,
            lastAutoRunFailedAt = card.LastAutoRunFailedAt,
            tag = card.TagName,
            commit = new
            {
                hash = commit.FullHash,
                shortHash = commit.ShortHash,
                isPushed = commit.IsPushed,
                url = $"https://github.com/{gitHubRepo}/commit/{commit.FullHash}",
            },
        };
        return JsonSerializer.Serialize(payload, s_camelCaseOpts);
    }

    private async Task<Card> BuildSampleCardAsync()
    {
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(
                $"ws-{Guid.NewGuid():N}"[..18],
                Path.Combine(Path.GetTempPath(), $"ws-{Guid.NewGuid():N}")), default);

        return await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Sample card", "body", TagName: "feature"), default);
    }

    private static bool TryWalk(
        JsonElement root,
        IReadOnlyList<string> segments,
        out string failedAt,
        out IReadOnlyList<string> availableKeys)
    {
        var current = root;
        foreach (var seg in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                failedAt = seg;
                availableKeys = Array.Empty<string>();
                return false;
            }
            if (!current.TryGetProperty(seg, out var next))
            {
                failedAt = seg;
                availableKeys = current.EnumerateObject().Select(p => p.Name).ToList();
                return false;
            }
            current = next;
        }
        failedAt = "";
        availableKeys = Array.Empty<string>();
        return true;
    }

    private static string? ClosestKey(string missing, IReadOnlyList<string> available)
    {
        if (available.Count == 0) return null;
        return available
            .Select(k => (Key: k, Distance: Levenshtein(missing, k)))
            .OrderBy(p => p.Distance)
            .First() is { Distance: <= 3 } best ? best.Key : null;
    }

    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }
        return d[a.Length, b.Length];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate the solution file walking up from {AppContext.BaseDirectory}.");
    }
}

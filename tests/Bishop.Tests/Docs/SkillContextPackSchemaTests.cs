using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.App.Git;
using Bishop.App.Git.GetRecentCommits;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Tags.ListTags;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.Docs;

/// <summary>
/// Enforces drift-resistance for backticked JSON-path references in the
/// <c>skills/bish-*/SKILL.md</c> docs. Every documented path starting with
/// <c>workspace.</c>, <c>git.</c>, <c>skill_specific.</c>, or
/// <c>conventions</c> must resolve in a real context-pack serialized with the
/// snake_case naming policy used by
/// <see cref="Bishop.Cli.Context.Pack.PrintContextPackCliCommand"/> and
/// <see cref="Bishop.App.Batches.RunBatch.RunBatchCommandHandler"/>.
/// Mirrors <see cref="ContextMdFactBlockTests"/> — see card #772.
/// </summary>
public sealed class SkillContextPackSchemaTests : IClassFixture<DbFixture>
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SkillsRoot = Path.Combine(RepoRoot, "skills");

    private static readonly JsonSerializerOptions s_snakeCaseOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    // Matches backticked tokens rooted at a known context-pack section.
    // Examples that match: `workspace.name`, `skill_specific.card.lane_name`,
    // `conventions["Shell selection"]`, `workspace.tags[].name`, `conventions`.
    private static readonly Regex s_pathRegex = new(
        @"`(?<path>(?:workspace|git|skill_specific|conventions)(?:\.[\w]+|\[[^\]`]+\]|\[\])*)`",
        RegexOptions.Compiled);

    private readonly IDbContextFactory<BishopDbContext> _factory;

    public SkillContextPackSchemaTests(DbFixture fixture) => _factory = fixture.Factory;

    [Theory]
    [InlineData("bish-work-on-card", "work-on-card")]
    [InlineData("bish-grill-cards", "grill-cards")]
    [InlineData("bish-triage", "triage")]
    [InlineData("bish-auto-card", "auto-card")]
    [InlineData("bish-spec-cards", "spec-cards")]
    // bish-write-skill is a meta-skill describing the pack schema for skill
    // authors; it only references universal roots (workspace, conventions).
    // grill-cards stands in for the skill_specific shape if any path snuck in.
    [InlineData("bish-write-skill", "grill-cards")]
    public async Task SkillMd_DocumentedJsonPaths_ExistInSerializedContextPack(
        string skillDirName, string providerSkillName)
    {
        var skillMdPath = Path.Combine(SkillsRoot, skillDirName, "SKILL.md");
        File.Exists(skillMdPath).Should().BeTrue($"expected SKILL.md at {skillMdPath}");

        var skillBody = File.ReadAllText(skillMdPath);
        var documentedPaths = ExtractDocumentedPaths(skillBody);
        documentedPaths.Should().NotBeEmpty(
            $"{skillDirName}/SKILL.md should reference at least one context-pack path");

        var pack = await BuildSamplePackAsync(providerSkillName);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(pack, s_snakeCaseOpts));
        var root = doc.RootElement;

        var failures = new List<string>();
        foreach (var path in documentedPaths)
        {
            var segments = ParsePathSegments(path);
            if (!TryWalk(root, segments, out var failedAt, out var availableKeys))
            {
                var closest = ClosestKey(failedAt, availableKeys);
                var hint = closest is null
                    ? $"available keys at that level: {string.Join(", ", availableKeys)}"
                    : $"did you mean '{closest}'? (available: {string.Join(", ", availableKeys)})";
                failures.Add($"  `{path}` — missing segment '{failedAt}'; {hint}");
            }
        }

        failures.Should().BeEmpty(
            $"every backticked JSON-path token in skills/{skillDirName}/SKILL.md must resolve in the serialized context-pack — " +
            $"PrintContextPackCliCommand and RunBatchCommandHandler use JsonNamingPolicy.SnakeCaseLower. " +
            $"Drift means agents hit KeyError at runtime. Mismatches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static IReadOnlyList<string> ExtractDocumentedPaths(string skillBody)
    {
        return s_pathRegex.Matches(skillBody)
            .Select(m => m.Groups["path"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> ParsePathSegments(string token)
    {
        var segments = new List<string>();
        var i = 0;
        while (i < token.Length)
        {
            var c = token[i];
            if (c == '.')
            {
                i++;
                continue;
            }
            if (c == '[')
            {
                var close = token.IndexOf(']', i);
                if (close < 0) break;
                var inner = token.Substring(i + 1, close - i - 1).Trim();
                i = close + 1;
                if (inner.Length == 0)
                {
                    // `[]` — array-of-anything; stop here so we just verify the parent array exists.
                    break;
                }
                if ((inner.StartsWith('"') && inner.EndsWith('"')) ||
                    (inner.StartsWith('\'') && inner.EndsWith('\'')))
                {
                    inner = inner[1..^1];
                }
                segments.Add(inner);
                continue;
            }
            var start = i;
            while (i < token.Length && token[i] != '.' && token[i] != '[') i++;
            segments.Add(token[start..i]);
        }
        return segments;
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

    private async Task<ContextPack> BuildSamplePackAsync(string providerSkillName)
    {
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(
                $"ws-{Guid.NewGuid():N}"[..18],
                Path.Combine(Path.GetTempPath(), $"ws-{Guid.NewGuid():N}")), default);

        // Seed a related card first, then a source card whose ### Related
        // section points at it — exercises the related_cards path for the
        // card-aware providers without affecting workspace-only providers.
        var related = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Related card", "", TagName: null), default);
        var source = await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Source card",
                $"### Why\nseed\n### Related\n- #{related.Number}", TagName: null), default);

        var provider = CreateProvider(providerSkillName);
        var sender = CreateSender();
        var handler = new BuildContextPackQueryHandler(new[] { provider }, StubGitCli(), sender);

        return await handler.Handle(
            new BuildContextPackQuery(providerSkillName, workspace, new ContextPackArgs(source.Number)),
            default);
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListLanesByWorkspaceQueryHandler()
                .Handle(call.ArgAt<ListLanesByWorkspaceQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<ListTagsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new ListTagsQueryHandler()
                .Handle(call.ArgAt<ListTagsQuery>(0), call.ArgAt<CancellationToken>(1)));
        sender.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetCardByNumberQueryHandler(_factory)
                .Handle(call.ArgAt<GetCardByNumberQuery>(0), call.ArgAt<CancellationToken>(1)));
        return sender;
    }

    private static IGitCli StubGitCli()
    {
        var git = Substitute.For<IGitCli>();
        git.GetCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("main"));
        git.GetRecentCommitsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GetRecentCommitsResult>(new GetRecentCommitsResult.Success(
                new List<CommitInfo>
                {
                    new("abc1234", "abc1234567890", "feat: seed", "", DateTimeOffset.UtcNow, false)
                },
                "origin/main")));
        return git;
    }

    private static IContextProvider CreateProvider(string skillName) => skillName switch
    {
        "work-on-card" => new WorkOnCardContextProvider(),
        "grill-cards" => new GrillCardsContextProvider(),
        "spec-cards" => new SpecCardsContextProvider(),
        "triage" => new TriageContextProvider(),
        "auto-card" => new AutoCardContextProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(skillName), skillName, "Unknown provider for drift test")
    };

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

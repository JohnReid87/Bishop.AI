using System.Text.RegularExpressions;
using Bishop.Core;
using FluentAssertions;

namespace Bishop.Tests.Docs;

/// <summary>
/// Enforces drift-resistance for load-bearing facts in the hand-written
/// <c>CONTEXT.md</c>. Tag/lane enumerations are wrapped in HTML-comment-delimited
/// fact-blocks (<c>&lt;!-- bishop-fact:NAME --&gt;</c> …
/// <c>&lt;!-- /bishop-fact --&gt;</c>) and must match the canonical sets defined in
/// code (<see cref="TagNames.All"/>, <see cref="SystemLaneNames.All"/>). A
/// code-to-code guard also pins <see cref="BrandTagPalette.DefaultColours"/> to
/// the same tag set so the generated <c>BISHOP_CONTEXT.md</c> stays consistent.
/// </summary>
public class ContextMdFactBlockTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ContextMdPath = Path.Combine(RepoRoot, "CONTEXT.md");

    [Fact]
    public void ContextMd_TagsFactBlock_MatchesTagNamesAll()
    {
        var items = ReadFactBlockItems("tags");

        items.Should().BeEquivalentTo(TagNames.All,
            "the <!-- bishop-fact:tags --> block in CONTEXT.md must match TagNames.All — " +
            "code is the source of truth (see the drift-resistance rule in CLAUDE.md)");
    }

    [Fact]
    public void ContextMd_LanesFactBlock_MatchesSystemLaneNamesAll()
    {
        var items = ReadFactBlockItems("lanes");

        items.Should().BeEquivalentTo(SystemLaneNames.All,
            "the <!-- bishop-fact:lanes --> block in CONTEXT.md must match SystemLaneNames.All — " +
            "code is the source of truth (see the drift-resistance rule in CLAUDE.md)");
    }

    [Fact]
    public void BrandTagPaletteDefaultColours_CoverExactlyTagNamesAll()
    {
        BrandTagPalette.DefaultColours.Keys.Should().BeEquivalentTo(TagNames.All,
            "BrandTagPalette.DefaultColours must cover exactly the tags in TagNames.All — " +
            "adding a tag without a colour (or vice versa) breaks the generated BISHOP_CONTEXT.md");
    }

    private static IReadOnlyList<string> ReadFactBlockItems(string blockName)
    {
        File.Exists(ContextMdPath)
            .Should().BeTrue($"expected CONTEXT.md at {ContextMdPath}");

        var content = File.ReadAllText(ContextMdPath);
        var blockPattern =
            $@"<!--\s*bishop-fact:{Regex.Escape(blockName)}\s*-->(?<body>.*?)<!--\s*/bishop-fact\s*-->";
        var match = Regex.Match(content, blockPattern, RegexOptions.Singleline);

        match.Success
            .Should().BeTrue(
                $"expected a <!-- bishop-fact:{blockName} --> … <!-- /bishop-fact --> block in CONTEXT.md");

        // Bullet items are written as `- ` followed by the value, optionally
        // wrapped in backticks. The capture strips the surrounding markdown
        // tokens so the test compares bare values to the code constant.
        var itemPattern = @"^\s*-\s+`?(?<value>[^`\r\n]+?)`?\s*$";
        var items = Regex.Matches(match.Groups["body"].Value, itemPattern, RegexOptions.Multiline)
            .Select(m => m.Groups["value"].Value.Trim())
            .ToList();

        items.Should().NotBeEmpty(
            $"<!-- bishop-fact:{blockName} --> block must contain at least one bullet item");

        return items;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any())
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the solution file walking up from {AppContext.BaseDirectory}.");
    }
}

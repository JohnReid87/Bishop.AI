using Bishop.App.Skills;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class SkillCommandRendererTests
{
    [Fact]
    public void Render_ReplacesWorkspacePath()
    {
        var result = SkillCommandRenderer.Render("path: {{workspace_path}}", null, null, null, @"C:\projects\myapp");

        result.Should().Be(@"path: C:\projects\myapp");
    }

    [Fact]
    public void Render_ReplacesCardNumber()
    {
        var result = SkillCommandRenderer.Render("card: {{card_number}}", 42, null, null, string.Empty);

        result.Should().Be("card: 42");
    }

    [Fact]
    public void Render_ReplacesCardTitle()
    {
        var result = SkillCommandRenderer.Render("title: {{card_title}}", null, "My Title", null, string.Empty);

        result.Should().Be("title: My Title");
    }

    [Fact]
    public void Render_ReplacesCardDescription()
    {
        var result = SkillCommandRenderer.Render("desc: {{card_description}}", null, null, "Some description", string.Empty);

        result.Should().Be("desc: Some description");
    }

    [Fact]
    public void Render_ReplacesAllTokensInOneTemplate()
    {
        var result = SkillCommandRenderer.Render(
            "/bish-work-on-card {{card_number}} --workspace {{workspace_path}} --title {{card_title}}",
            7, "Fix bug", null, @"C:\repo");

        result.Should().Be("/bish-work-on-card 7 --workspace C:\\repo --title Fix bug");
    }

    [Fact]
    public void Render_WhenCardNumberIsNull_SubstitutesEmptyString()
    {
        var result = SkillCommandRenderer.Render("{{card_number}}", null, null, null, string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Render_WhenCardTitleIsNull_SubstitutesEmptyString()
    {
        var result = SkillCommandRenderer.Render("{{card_title}}", null, null, null, string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Render_WhenCardDescriptionIsNull_SubstitutesEmptyString()
    {
        var result = SkillCommandRenderer.Render("{{card_description}}", null, null, null, string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Render_WhenAllCardParamsAreNull_LeavesNonCardTokensIntact()
    {
        var result = SkillCommandRenderer.Render("{{workspace_path}}", null, null, null, @"C:\ws");

        result.Should().Be(@"C:\ws");
    }

    [Fact]
    public void Render_UnknownToken_PassesThroughUnchanged()
    {
        var result = SkillCommandRenderer.Render("{{unknown_token}}", 1, "t", "d", @"C:\ws");

        result.Should().Be("{{unknown_token}}");
    }

    [Fact]
    public void Render_TemplateWithNoTokens_ReturnsTemplateUnchanged()
    {
        var result = SkillCommandRenderer.Render("claude --version", 1, "t", "d", @"C:\ws");

        result.Should().Be("claude --version");
    }

    [Theory]
    [InlineData("&", "repro  calc.exe")]
    [InlineData("|", "repro  calc.exe")]
    [InlineData("<", "repro  calc.exe")]
    [InlineData(">", "repro  calc.exe")]
    [InlineData("^", "repro  calc.exe")]
    public void Render_CardTitleWithShellMetachar_MetacharStripped(string metaChar, string expected)
    {
        var result = SkillCommandRenderer.Render("{{card_title}}", null, $"repro {metaChar} calc.exe", null, string.Empty);

        result.Should().Be(expected);
    }

    [Fact]
    public void Render_CardTitleWithNewline_NewlineReplacedWithSpace()
    {
        var result = SkillCommandRenderer.Render("{{card_title}}", null, "line1\nline2", null, string.Empty);

        result.Should().Be("line1 line2");
    }

    [Theory]
    [InlineData("&")]
    [InlineData("|")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("^")]
    public void Render_CardDescriptionWithShellMetachar_MetacharStripped(string metaChar)
    {
        var result = SkillCommandRenderer.Render("{{card_description}}", null, null, $"desc {metaChar} attack", string.Empty);

        result.Should().Be("desc  attack");
    }

    [Fact]
    public void Render_WorkspacePathMetacharsAreNotSanitized()
    {
        // workspace_path is a trusted internal value; metacharacters in directory names must survive.
        var result = SkillCommandRenderer.Render("{{workspace_path}}", null, null, null, @"C:\repos\my&project");

        result.Should().Be(@"C:\repos\my&project");
    }

    // ── Direct Sanitize coverage ──────────────────────────────────────────────

    [Theory]
    [InlineData("--flag & calc.exe", "--flag  calc.exe")]
    [InlineData("--flag | bad", "--flag  bad")]
    [InlineData("value <input", "value input")]
    [InlineData("> output", " output")]
    [InlineData("foo^bar", "foobar")]
    public void Sanitize_ShellMetacharsAreStripped(string input, string expected)
    {
        SkillCommandRenderer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_NewlineReplacedWithSpace()
    {
        SkillCommandRenderer.Sanitize("line1\nline2").Should().Be("line1 line2");
    }

    [Fact]
    public void Sanitize_PlainTextUnchanged()
    {
        SkillCommandRenderer.Sanitize("--verbose --output file.txt").Should().Be("--verbose --output file.txt");
    }
}

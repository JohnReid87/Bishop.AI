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
}

using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class SkillTagMapTests
{
    [Theory]
    [InlineData("bish-arch", "arch")]
    [InlineData("bish-security", "security")]
    [InlineData("bish-tests", "test")]
    [InlineData("bish-coverage", "test")]
    [InlineData("bish-dead-code", "chore")]
    [InlineData("bish-audit-docs", "docs")]
    [InlineData("bish-triage", "bug")]
    public void GetTag_MapsKnownSkills(string skill, string expected)
        => new SkillTagMap().GetTag(skill).Should().Be(expected);

    [Theory]
    [InlineData("bish-grill-cards")]
    [InlineData("bish-onboard")]
    [InlineData("")]
    [InlineData("unrelated-skill")]
    public void GetTag_ReturnsNullForUnmappedSkill(string skill)
        => new SkillTagMap().GetTag(skill).Should().BeNull();

    [Fact]
    public void GetTag_IsCaseInsensitive()
        => new SkillTagMap().GetTag("BISH-ARCH").Should().Be("arch");
}

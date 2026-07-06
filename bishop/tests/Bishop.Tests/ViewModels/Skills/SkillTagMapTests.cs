using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Skills;

public class SkillTagMapTests
{
    [Theory]
    [InlineData("bish-arch", "arch")]
    [InlineData("bish-security", "security")]
    [InlineData("bish-tests", "test")]
    [InlineData("bish-coverage", "test")]
    [InlineData("bish-dead-code", "chore")]
    [InlineData("bish-audit-docs", "docs")]
    public void GetTag_MapsKnownSkills(string skill, string expected)
        => SkillTagMap.GetTag(skill).Should().Be(expected);

    [Theory]
    [InlineData("bish-grill-cards")]
    [InlineData("bish-onboard")]
    [InlineData("")]
    [InlineData("unrelated-skill")]
    public void GetTag_ReturnsNullForUnmappedSkill(string skill)
        => SkillTagMap.GetTag(skill).Should().BeNull();

    [Fact]
    public void GetTag_IsCaseInsensitive()
        => SkillTagMap.GetTag("BISH-ARCH").Should().Be("arch");
}

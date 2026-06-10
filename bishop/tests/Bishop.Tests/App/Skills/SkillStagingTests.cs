using Bishop.App.Skills;
using Bishop.Core.Skills;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class SkillStagingTests
{
    [Fact]
    public void ShouldShowStageDialog_WhenStageIsTrueAndNoCard_ReturnsTrue()
    {
        var skill = new InstalledSkill("s", "", [], null, Stage: true);

        SkillStaging.ShouldShowStageDialog(skill, hasCard: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldShowStageDialog_WhenStageIsTrueAndCardProvided_ReturnsFalse()
    {
        var skill = new InstalledSkill("s", "", [], null, Stage: true);

        SkillStaging.ShouldShowStageDialog(skill, hasCard: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowStageDialog_WhenStageIsFalseAndNoCard_ReturnsFalse()
    {
        var skill = new InstalledSkill("s", "", [], null, Stage: false);

        SkillStaging.ShouldShowStageDialog(skill, hasCard: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowStageDialog_WhenStageIsFalseAndCardProvided_ReturnsFalse()
    {
        var skill = new InstalledSkill("s", "", [], null, Stage: false);

        SkillStaging.ShouldShowStageDialog(skill, hasCard: true).Should().BeFalse();
    }
}

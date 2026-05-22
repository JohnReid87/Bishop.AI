using Bishop.App.Skills;
using Bishop.Core.Skills;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class SkillMenuBuilderTests
{
    [Fact]
    public void Build_WhenNoSkills_ReturnsEmpty()
    {
        var result = SkillMenuBuilder.Build([], "card");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_WhenNoSkillsMatchScope_ReturnsEmpty()
    {
        var skills = new[]
        {
            new InstalledSkill("a", "", ["workspace"], "claude /a"),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_WhenSkillHasNullCommand_ExcludesIt()
    {
        var skills = new[]
        {
            new InstalledSkill("a", "", ["card"], null),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_SingleMatchingSkill_ReturnsSingleItemWithNoSeparator()
    {
        var skill = new InstalledSkill("my-skill", "", ["card"], "claude /my-skill");
        var skills = new[] { skill };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("my-skill");
        result[0].Skill.Should().Be(skill);
        result[0].HasSeparatorAfter.Should().BeFalse();
    }

    [Fact]
    public void Build_MultipleSkills_LastItemHasNoSeparator()
    {
        var skills = new[]
        {
            new InstalledSkill("a", "", ["card"], "claude /a"),
            new InstalledSkill("b", "", ["card"], "claude /b"),
            new InstalledSkill("c", "", ["card"], "claude /c"),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(3);
        result[0].HasSeparatorAfter.Should().BeTrue();
        result[1].HasSeparatorAfter.Should().BeTrue();
        result[2].HasSeparatorAfter.Should().BeFalse();
    }

    [Fact]
    public void Build_MultipleSkills_PreservesInputOrder()
    {
        var skills = new[]
        {
            new InstalledSkill("first",  "", ["card"], "claude /first"),
            new InstalledSkill("second", "", ["card"], "claude /second"),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Select(m => m.Name).Should().Equal("first", "second");
    }

    [Fact]
    public void Build_FiltersToRequestedScope()
    {
        var skills = new[]
        {
            new InstalledSkill("card-skill",      "", ["card"],      "claude /card-skill"),
            new InstalledSkill("workspace-skill",  "", ["workspace"], "claude /workspace-skill"),
        };

        var cardResult      = SkillMenuBuilder.Build(skills, "card");
        var workspaceResult = SkillMenuBuilder.Build(skills, "workspace");

        cardResult.Should().ContainSingle(m => m.Name == "card-skill");
        workspaceResult.Should().ContainSingle(m => m.Name == "workspace-skill");
    }

    [Fact]
    public void Build_SkillWithMultipleScopes_IncludedForEachMatchingScope()
    {
        var skill = new InstalledSkill("multi", "", ["card", "workspace"], "claude /multi");

        var cardResult      = SkillMenuBuilder.Build([skill], "card");
        var workspaceResult = SkillMenuBuilder.Build([skill], "workspace");

        cardResult.Should().ContainSingle();
        workspaceResult.Should().ContainSingle();
    }
}

using Bishop.App.Skills;
using Bishop.Core.Skills;
using FluentAssertions;
using static Bishop.Core.Skills.SkillCategory;

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
    public void Build_SingleMatchingSkill_ReturnsSingleItem()
    {
        var skill = new InstalledSkill("my-skill", "", ["card"], "claude /my-skill");
        var skills = new[] { skill };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("my-skill");
        result[0].Skill.Should().Be(skill);
    }

    [Fact]
    public void Build_MultipleSkillsInSameGroup_ReturnsAllItems()
    {
        var skills = new[]
        {
            new InstalledSkill("a", "", ["card"], "claude /a"),
            new InstalledSkill("b", "", ["card"], "claude /b"),
            new InstalledSkill("c", "", ["card"], "claude /c"),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Build_MultipleSkills_SortedAlphabeticallyWithinGroup()
    {
        var skills = new[]
        {
            new InstalledSkill("second", "", ["card"], "claude /second"),
            new InstalledSkill("first",  "", ["card"], "claude /first"),
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

    [Fact]
    public void Build_SkillsAcrossTwoGroups_ReturnsBothItems()
    {
        var skills = new[]
        {
            new InstalledSkill("review-skill", "", ["card"], "claude /review-skill", Category: Review),
            new InstalledSkill("exec-skill",   "", ["card"], "claude /exec-skill",   Category: Execute),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Build_GroupHeader_SetOnFirstItemOfEachGroup()
    {
        var skills = new[]
        {
            new InstalledSkill("b-review", "", ["card"], "claude /b-review", Category: Review),
            new InstalledSkill("a-review", "", ["card"], "claude /a-review", Category: Review),
            new InstalledSkill("exec",     "", ["card"], "claude /exec",     Category: Execute),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(3);
        result[0].GroupHeader.Should().Be("REVIEW");
        result[1].GroupHeader.Should().BeNull();
        result[2].GroupHeader.Should().Be("EXECUTE");
    }

    [Fact]
    public void Build_FixedCategoryOrder_ReviewBeforeDiscussBeforeExecuteBeforeSetupBeforeOther()
    {
        var skills = new[]
        {
            new InstalledSkill("other",     "", ["card"], "claude /other",     Category: Other),
            new InstalledSkill("setup",     "", ["card"], "claude /setup",     Category: Setup),
            new InstalledSkill("execution", "", ["card"], "claude /execution", Category: Execute),
            new InstalledSkill("discuss",   "", ["card"], "claude /discuss",   Category: Discuss),
            new InstalledSkill("review",    "", ["card"], "claude /review",    Category: Review),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Select(m => m.Name).Should().Equal("review", "discuss", "execution", "setup", "other");
    }

    [Fact]
    public void Build_MultipleSkillsInGroup_ReturnsAllItems()
    {
        var skills = new[]
        {
            new InstalledSkill("r1", "", ["card"], "claude /r1", Category: Review),
            new InstalledSkill("r2", "", ["card"], "claude /r2", Category: Review),
            new InstalledSkill("e1", "", ["card"], "claude /e1", Category: Execute),
        };

        var result = SkillMenuBuilder.Build(skills, "card");

        result.Should().HaveCount(3);
    }

}

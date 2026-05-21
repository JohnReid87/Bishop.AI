using Bishop.App.Skills.DiscoverSkills;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class DiscoverSkillsQueryHandlerTests : IDisposable
{
    private readonly string _skillsRoot;

    public DiscoverSkillsQueryHandlerTests()
    {
        _skillsRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_skillsRoot);
    }

    public void Dispose()
    {
        Directory.Delete(_skillsRoot, recursive: true);
    }

    private static void WriteSkillMd(string dir, string content)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }

    private DiscoverSkillsQueryHandler CreateSut() => new(_skillsRoot);

    [Fact]
    public async Task Handle_SkillsRootDoesNotExist_ReturnsEmpty()
    {
        // Arrange
        var nonExistent = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid());
        var sut = new DiscoverSkillsQueryHandler(nonExistent);

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkillDirHasNoSkillMd_ReturnsEmpty()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_skillsRoot, "my-skill"));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkillMdHasNoFrontmatter_ReturnsEmpty()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "Just some content\nno frontmatter here");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkillMdMissingName_ReturnsEmpty()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\ndescription: A skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkillMdWithNameOnly_ReturnsSkillWithDefaults()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var skill = result[0];
        skill.Name.Should().Be("my-skill");
        skill.Description.Should().BeEmpty();
        skill.Scope.Should().BeNull();
        skill.Command.Should().BeNull();
        skill.Stage.Should().BeFalse();
        skill.StagePrompt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SkillMdWithAllFields_ReturnsFullyPopulatedSkill()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\ndescription: Does something useful\nbishop.scope: card\nbishop.command: /my-skill {{card_number}}\nbishop.stage: true\nbishop.stage_prompt: Enter a card number\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var skill = result[0];
        skill.Name.Should().Be("my-skill");
        skill.Description.Should().Be("Does something useful");
        skill.Scope.Should().Be("card");
        skill.Command.Should().Be("/my-skill {{card_number}}");
        skill.Stage.Should().BeTrue();
        skill.StagePrompt.Should().Be("Enter a card number");
    }

    [Fact]
    public async Task Handle_StageFieldIsUppercaseTrue_SetsStageTrueIgnoringCase()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.stage: TRUE\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Stage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyScopeAndCommand_ReturnsNullScopeAndCommand()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.scope: \nbishop.command: \n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeNull();
        result[0].Command.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MultipleSkillDirs_ReturnsAllValidSkills()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "skill-a"), "---\nname: skill-a\n---\n");
        WriteSkillMd(Path.Combine(_skillsRoot, "skill-b"), "---\nname: skill-b\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(s => s.Name).Should().BeEquivalentTo(["skill-a", "skill-b"]);
    }

    [Fact]
    public async Task Handle_MixOfValidAndInvalidSkillDirs_ReturnsOnlyValidSkills()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "valid-skill"), "---\nname: valid-skill\n---\n");
        Directory.CreateDirectory(Path.Combine(_skillsRoot, "no-skill-md"));
        WriteSkillMd(Path.Combine(_skillsRoot, "missing-name"), "---\ndescription: no name here\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("valid-skill");
    }

    [Fact]
    public async Task Handle_WhitespaceOnlyName_ReturnsEmpty()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname:   \n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MalformedFrontmatterLineWithNoColon_SkipsLineAndReturnsSkill()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nbadkey\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("my-skill");
    }

    [Fact]
    public async Task Handle_UppercaseFrontmatterKey_ParsedCaseInsensitively()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nName: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("my-skill");
    }

    [Fact]
    public async Task ParameterlessConstructor_ResolvesUserProfileSkillsPath_DoesNotThrow()
    {
        // Arrange — exercises the hardcoded ~/.claude/skills path resolution
        var sut = new DiscoverSkillsQueryHandler();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

using System.Reflection;
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
        skill.Scope.Should().BeEmpty();
        skill.Command.Should().BeNull();
        skill.Stage.Should().BeFalse();
        skill.StagePrompt.Should().BeNull();
        skill.StagePrefill.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SkillMdWithAllFields_ReturnsFullyPopulatedSkill()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\ndescription: Does something useful\nbishop.scope: card\nbishop.command: /my-skill {{card_number}}\nbishop.stage: true\nbishop.stage_prompt: Enter a card number\nbishop.stage_prefill: \"{{card_title}}\\n\\n{{card_description}}\"\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var skill = result[0];
        skill.Name.Should().Be("my-skill");
        skill.Description.Should().Be("Does something useful");
        skill.Scope.Should().BeEquivalentTo(["card"]);
        skill.Command.Should().Be("/my-skill {{card_number}}");
        skill.Stage.Should().BeTrue();
        skill.StagePrompt.Should().Be("Enter a card number");
        skill.StagePrefill.Should().Be("{{card_title}}\n\n{{card_description}}");
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
    public async Task Handle_EmptyScopeAndCommand_ReturnsEmptyScopeAndNullCommand()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.scope: \nbishop.command: \n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeEmpty();
        result[0].Command.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ScopeWithMultipleCommaSeparatedValues_ReturnsSplitList()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.scope: card,workspace\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeEquivalentTo(["card", "workspace"]);
    }

    [Fact]
    public async Task Handle_ScopeWithCommaSeparatedValuesAndSpaces_TrimsEntries()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.scope: card , workspace\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeEquivalentTo(["card", "workspace"]);
    }

    [Fact]
    public async Task Handle_ScopeWithCommaSeparatedValuesAndEmptySegments_DropsEmpties()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.scope: card,,workspace,\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeEquivalentTo(["card", "workspace"]);
    }

    [Fact]
    public async Task Handle_StagePrefillQuotedWithNewlineEscapes_ConvertsToNewlines()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\nbishop.stage_prefill: \"line1\\nline2\"\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StagePrefill.Should().Be("line1\nline2");
    }

    [Fact]
    public async Task Handle_StagePrefillUnquoted_ReturnedAsLiteral()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\nbishop.stage_prefill: literal\\nvalue\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StagePrefill.Should().Be("literal\\nvalue");
    }

    [Fact]
    public async Task Handle_EmptyStagePrefill_ReturnsNull()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.stage_prefill: \n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StagePrefill.Should().BeNull();
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
    public async Task Handle_FrontmatterLineWithColonAtIndexZero_SkipsLineAndReturnsSkill()
    {
        // Arrange — colonIdx == 0 triggers the colonIdx <= 0 guard in ParseFrontmatter
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\n: value\nname: my-skill\n---\n");
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
    public void ParameterlessConstructor_ResolvesUserProfileSkillsPath()
    {
        // Arrange
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "skills");

        // Act
        var sut = new DiscoverSkillsQueryHandler();
        var field = typeof(DiscoverSkillsQueryHandler)
            .GetField("_skillsRoot", BindingFlags.NonPublic | BindingFlags.Instance);
        var actual = (string)field!.GetValue(sut)!;

        // Assert
        actual.Should().Be(expected);
    }
}

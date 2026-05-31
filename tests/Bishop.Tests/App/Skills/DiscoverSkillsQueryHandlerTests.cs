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
        skill.MarkdownBody.Should().BeEmpty();
        skill.SourcePath.Should().Be(Path.Combine(_skillsRoot, "my-skill", "SKILL.md"));
    }

    [Fact]
    public async Task Handle_SkillMdWithBody_ReturnsRawBodyAfterFrontmatter()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\n---\n# Heading\n\nSome **body** text.\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].MarkdownBody.Should().Be("# Heading\n\nSome **body** text.\n");
    }

    [Fact]
    public async Task Handle_SkillMdWithNoBodyAfterFrontmatter_ReturnsEmptyBody()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].MarkdownBody.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyFile_ReturnsEmpty()
    {
        // Arrange - splitting "" yields a single-element array; lines.Length < 2 short-circuits parsing
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SingleLineFile_ReturnsEmpty()
    {
        // Arrange - "---" splits to one element; lines.Length < 2 short-circuits parsing
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FirstLineNotOpeningFence_ReturnsEmpty()
    {
        // Arrange - lines[0] != "---" so ParseFrontmatterAndBody returns an empty frontmatter immediately
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "name: my-skill\n---\ndescription: a skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ClosingFenceIsLastLine_ReturnsEmptyBody()
    {
        // Arrange - closing "---" is at lines[lines.Length - 1]; the condition
        // closingIndex < lines.Length - 1 evaluates to false, so body is empty
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\ndescription: test\n---");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].MarkdownBody.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleClosingFences_BodyStartsAfterFirstFence()
    {
        // Arrange - the loop breaks at the first "---"; subsequent "---" lines appear in the body
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\n---\nbody line\n---\nmore body\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].MarkdownBody.Should().Be("body line\n---\nmore body\n");
    }

    [Fact]
    public async Task Handle_SkillMdWithAllFields_ReturnsFullyPopulatedSkill()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\ndescription: Does something useful\nbishop.scope: card\nbishop.command: /my-skill {{card_number}}\nbishop.stage: true\nbishop.stage_prompt: Enter a card number\nbishop.stage_prefill: \"{{card_title}}\\n\\n{{card_description}}\"\nbishop.stage_projects: true\nbishop.stage_file_picker: true\n---\n");
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
        skill.StageProjects.Should().BeTrue();
        skill.StageFilePicker.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StageProjectsMissing_DefaultsToFalse()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StageProjects.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StageProjectsUppercaseTrue_SetsStageProjectsTrueIgnoringCase()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.stage_projects: TRUE\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StageProjects.Should().BeTrue();
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
    public async Task Handle_ScopeWithWhitespaceOnlySegments_DropsWhitespaceSegments()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.scope: card, ,workspace\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeEquivalentTo(["card", "workspace"]);
    }

    [Fact]
    public async Task Handle_MissingScopeField_ParsesNullAsEmptyList()
    {
        // Arrange - no bishop.scope key → TryGetValue sets scope to null → ParseScope(null) → []
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Scope.Should().BeEmpty();
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
    public async Task Handle_MissingStagePrefillField_ParsesNullAsNull()
    {
        // Arrange - no bishop.stage_prefill key → TryGetValue sets stagePrefill to null → ParseStagePrefill(null) → null
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StagePrefill.Should().BeNull();
    }

    [Fact]
    public async Task Handle_StagePrefillSingleDoubleQuote_ReturnedAsLiteral()
    {
        // Arrange — length-1 quoted value does not meet the >= 2 threshold so the unquoting branch is skipped
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.stage_prefill: \"\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StagePrefill.Should().Be("\"");
    }

    [Fact]
    public async Task Handle_StagePrefillTwoDoubleQuotes_UnquotedToEmptyString()
    {
        // Arrange — "" meets both the length and quote checks; unquoted slice [1..^1] is empty
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.stage_prefill: \"\"\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StagePrefill.Should().BeEmpty();
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
    public async Task Handle_KnownCategory_ParsedToCorrectEnum()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\nbishop.category: review\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Category.Should().Be(Bishop.Core.Skills.SkillCategory.Review);
    }

    [Theory]
    [InlineData("Review")]
    [InlineData("REVIEW")]
    [InlineData("  review  ")]
    public async Task Handle_CategoryVariantCasing_MapsToSameEnum(string rawCategory)
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            $"---\nname: my-skill\nbishop.category: {rawCategory}\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Category.Should().Be(Bishop.Core.Skills.SkillCategory.Review);
    }

    [Fact]
    public async Task Handle_MissingCategory_DefaultsToOther()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Category.Should().Be(Bishop.Core.Skills.SkillCategory.Other);
    }

    [Fact]
    public async Task Handle_UnknownCategory_DefaultsToOther()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            "---\nname: my-skill\nbishop.category: totally-made-up\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Category.Should().Be(Bishop.Core.Skills.SkillCategory.Other);
    }

    [Fact]
    public async Task Handle_MissingCategoryField_ParsesNullAsOther()
    {
        // Arrange - no bishop.category key → TryGetValue sets category to null → ParseCategory(null) → Other
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Category.Should().Be(Bishop.Core.Skills.SkillCategory.Other);
    }

    [Theory]
    [InlineData("discuss",   Bishop.Core.Skills.SkillCategory.Discuss)]
    [InlineData("code",      Bishop.Core.Skills.SkillCategory.Code)]
    [InlineData("tests",     Bishop.Core.Skills.SkillCategory.Tests)]
    [InlineData("review",    Bishop.Core.Skills.SkillCategory.Review)]
    [InlineData("execute",   Bishop.Core.Skills.SkillCategory.Execute)]
    [InlineData("setup",     Bishop.Core.Skills.SkillCategory.Setup)]
    [InlineData("meta",      Bishop.Core.Skills.SkillCategory.Meta)]
    [InlineData("other",     Bishop.Core.Skills.SkillCategory.Other)]
    public async Task Handle_AllCategoryValues_ParsedCorrectly(string raw, Bishop.Core.Skills.SkillCategory expected)
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"),
            $"---\nname: my-skill\nbishop.category: {raw}\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].Category.Should().Be(expected);
    }

    [Fact]
    public async Task ParameterlessConstructor_ResolvedPathIsUsedForHandling()
    {
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "skills");

        var sut = new DiscoverSkillsQueryHandler();

        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);
        result.Should().NotBeNull();
        result.Should().AllSatisfy(s => s.SourcePath.Should().StartWith(expectedPath));
    }

    [Fact]
    public async Task ParameterlessConstructor_DefaultPathAbsent_ReturnsEmpty()
    {
        // Arrange - construct via parameterless ctor, then point _skillsRoot at a guaranteed-absent path
        var sut = new DiscoverSkillsQueryHandler();
        var field = typeof(DiscoverSkillsQueryHandler)
            .GetField("_skillsRoot", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(sut, Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid()));

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_StageFilePickerMissing_DefaultsToFalse()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StageFilePicker.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StageFilePickerUppercaseTrue_SetsStageFilePickerTrueIgnoringCase()
    {
        // Arrange
        WriteSkillMd(Path.Combine(_skillsRoot, "my-skill"), "---\nname: my-skill\nbishop.stage_file_picker: TRUE\n---\n");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DiscoverSkillsQuery(), CancellationToken.None);

        // Assert
        result[0].StageFilePicker.Should().BeTrue();
    }

}

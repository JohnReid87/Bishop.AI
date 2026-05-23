using Bishop.App.Context;
using FluentAssertions;

namespace Bishop.Tests.App.Context;

public sealed class PrintContextQueryHandlerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly PrintContextQueryHandler _handler = new();
    private readonly string _bishopDir;
    private readonly string _contextFilePath;

    private const string SampleContent = """
        # BISHOP_CONTEXT — test-ws

        Intro text.

        ## This workspace

        - **Name:** test-ws

        ## Shell selection (STABLE)

        Use PowerShell for cmdlets, Bash for POSIX tools.

        ## Card Push Procedure (STABLE)

        Push a card via heredoc.

        ## Card model

        Cards have a number and a title.
        """;

    public PrintContextQueryHandlerTests()
    {
        _bishopDir = Path.Combine(_tempDir, ".bishop");
        Directory.CreateDirectory(_bishopDir);
        _contextFilePath = Path.Combine(_bishopDir, "BISHOP_CONTEXT.md");
        File.WriteAllText(_contextFilePath, SampleContent);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task Handle_NoSection_ReturnsFulContentWithHintHeader()
    {
        var result = await _handler.Handle(new PrintContextQuery(_tempDir), default);

        result.Should().StartWith("# Sections:");
        result.Should().Contain("This workspace");
        result.Should().Contain("Shell selection");
        result.Should().Contain("Card Push Procedure");
        result.Should().Contain("Card model");
        result.Should().Contain("bishop context print --section");
        result.Should().Contain("# BISHOP_CONTEXT — test-ws");
    }

    [Fact]
    public async Task Handle_SectionName_ReturnsOnlyThatSection()
    {
        var result = await _handler.Handle(new PrintContextQuery(_tempDir, "Shell selection"), default);

        result.Should().Contain("## Shell selection (STABLE)");
        result.Should().Contain("Use PowerShell for cmdlets");
        result.Should().NotContain("## Card Push Procedure");
        result.Should().NotContain("# Sections:");
    }

    [Fact]
    public async Task Handle_SectionNameCaseInsensitive_Matches()
    {
        var result = await _handler.Handle(new PrintContextQuery(_tempDir, "card push procedure"), default);

        result.Should().Contain("## Card Push Procedure (STABLE)");
    }

    [Fact]
    public async Task Handle_SectionNameWithoutLabel_MatchesSectionWithLabel()
    {
        var result = await _handler.Handle(new PrintContextQuery(_tempDir, "Card model"), default);

        result.Should().Contain("## Card model");
        result.Should().Contain("Cards have a number");
    }

    [Fact]
    public async Task Handle_UnknownSection_ThrowsWithValidNames()
    {
        var act = () => _handler.Handle(new PrintContextQuery(_tempDir, "Nonexistent"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown section*Nonexistent*")
            .WithMessage("*Shell selection*");
    }

    [Fact]
    public async Task Handle_MissingFile_ThrowsWithHelpfulMessage()
    {
        var missingDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var act = () => _handler.Handle(new PrintContextQuery(missingDir), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Context file not found*");
    }

    [Fact]
    public void ParseH2Sections_StripLabelFromStableSection()
    {
        var sections = PrintContextQueryHandler.ParseH2Sections(SampleContent);

        sections.Should().Contain(s => s.Name == "Shell selection");
        sections.Should().Contain(s => s.Name == "Card Push Procedure");
        sections.Should().Contain(s => s.Name == "Card model");
        sections.Should().NotContain(s => s.Name.Contains("STABLE"));
    }
}

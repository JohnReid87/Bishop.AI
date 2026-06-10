using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills.LaunchSkill;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels.Workspaces;

public sealed class BoardSkillsCoordinatorTests
{
    private static BoardSkillsCoordinator CreateSut(ISender? mediator = null)
    {
        return new BoardSkillsCoordinator(
            mediator ?? Substitute.For<ISender>(),
            Substitute.For<IAppSettings>(),
            () => @"C:\workspace");
    }

    private static SkillLaunchItem Item(string renderedCommand = "/skill-cmd") =>
        new("test", null, "", renderedCommand, false, null, null, "");

    // ── CWE-78: stagedText metachar sanitization ──────────────────────────────
    // stagedText from SkillStageDialog must have cmd.exe metacharacters stripped
    // before being appended to the rendered command; otherwise `--flag & calc.exe`
    // would let cmd.exe /k run calc.exe as a second shell command.

    [Theory]
    [InlineData("--flag & calc.exe", "/skill-cmd --flag  calc.exe")]
    [InlineData("--flag | bad", "/skill-cmd --flag  bad")]
    [InlineData("arg <input", "/skill-cmd arg input")]
    [InlineData("> output", "/skill-cmd  output")]
    [InlineData("foo^bar", "/skill-cmd foobar")]
    public async Task LaunchAsync_StagedTextWithShellMetachar_MetacharStripped(
        string stagedText, string expectedCommand)
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var sut = CreateSut(mediator);

        // Act
        await sut.LaunchAsync(Item(), stagedText, new TerminalSnap(0, 0, 800, 600), "model");

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == expectedCommand),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_NullStagedText_SendsRenderedCommandUnchanged()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var sut = CreateSut(mediator);

        // Act
        await sut.LaunchAsync(Item("/bish-work-on-card #42"), null, default, "model");

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "/bish-work-on-card #42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAsync_WhitespaceStagedText_SendsRenderedCommandUnchanged()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var sut = CreateSut(mediator);

        // Act
        await sut.LaunchAsync(Item("/bish-work-on-card #42"), "   ", default, "model");

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<LaunchSkillCommand>(c => c.RenderedCommand == "/bish-work-on-card #42"),
            Arg.Any<CancellationToken>());
    }
}

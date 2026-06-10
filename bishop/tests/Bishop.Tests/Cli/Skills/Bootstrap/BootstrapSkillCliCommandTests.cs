using Bishop.App.Skills.GetSkillBootstrapInfo;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Bootstrap;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;
using System.Text.Json;

namespace Bishop.Tests.Cli.Skills.Bootstrap;

[Collection("ConsoleTests")]
public sealed class BootstrapSkillCliCommandTests
{
    private static (IMediator mediator, BootstrapSkillCliCommand cmd) BuildWithInfo(
        SkillBootstrapInfo info)
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = info.WorkspaceName, Path = Directory.GetCurrentDirectory() };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetSkillBootstrapInfoQuery>(), Arg.Any<CancellationToken>())
            .Returns(info);
        return (mediator, new BootstrapSkillCliCommand(mediator));
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_PrintsWorkspaceInfoToStdout()
    {
        var cwd = Directory.GetCurrentDirectory();
        var info = new SkillBootstrapInfo(
            "test-ws", cwd,
            [new TagInfo("feature", "#ff0000"), new TagInfo("bug", "#0000ff")],
            [new LaneInfo("To Do", 1), new LaneInfo("Doing", 2)]);
        var (mediator, cmd) = BuildWithInfo(info);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync([]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        var text = output.ToString();
        text.Should().Contain("Workspace: test-ws");
        text.Should().Contain($"Path:      {cwd}");
        text.Should().Contain("Tags:      feature, bug");
        text.Should().Contain("Lanes:     To Do, Doing");
        await mediator.Received(1).Send(Arg.Any<GetSkillBootstrapInfoQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsValidJsonWithBootstrapFields()
    {
        var cwd = Directory.GetCurrentDirectory();
        var info = new SkillBootstrapInfo(
            "test-ws", cwd,
            [new TagInfo("feature", "#ff0000")],
            [new LaneInfo("To Do", 1)]);
        var (_, cmd) = BuildWithInfo(info);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        var root = doc.RootElement;
        root.GetProperty("workspaceName").GetString().Should().Be("test-ws");
        root.GetProperty("workspacePath").GetString().Should().Be(cwd);
        root.TryGetProperty("tags", out var tags).Should().BeTrue();
        tags.GetArrayLength().Should().Be(1);
        root.TryGetProperty("lanes", out var lanes).Should().BeTrue();
        lanes.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_WorkspaceNotFound_ExitsOneAndWritesErrorToStderr()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new BootstrapSkillCliCommand(mediator);
        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync([]); }
        finally { Console.SetError(originalErr); }

        exitCode.Should().Be(1);
        errorOutput.ToString().Should().Contain("Not in a Bishop workspace");
    }

    [Fact]
    public async Task InvokeAsync_EmptyTagsAndLanes_TextOutputRendersValidLines()
    {
        var cwd = Directory.GetCurrentDirectory();
        var info = new SkillBootstrapInfo(
            "test-ws", cwd,
            [],
            []);
        var (_, cmd) = BuildWithInfo(info);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync([]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        var text = output.ToString();
        text.Should().Contain("Tags:      ");
        text.Should().Contain("Lanes:     ");
    }
}

using Bishop.App.Workspaces.InitWorkspace;
using Bishop.Cli.Workspaces.Init;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.Init;

[Collection("EnvVar")]
public sealed class InitWorkspaceCliCommandTests
{
    private static Workspace MakeWorkspace(string name = "test-ws", string path = @"C:\test", string? gitHubRepo = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Path = path,
        GitHubRepo = gitHubRepo
    };

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsInitWorkspaceCommand()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: true, GitHubLinked: false));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--path", @"C:\test", "--name", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AlreadyInitialized_PrintsAlreadyInitializedMessage()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: false, GitHubLinked: false));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--path", @"C:\test"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        output.ToString().Should().Contain("is already initialized");
    }

    [Fact]
    public async Task InvokeAsync_RestoredResult_PrintsRestoredMessage()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: false, GitHubLinked: false, Restored: true));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--path", @"C:\test"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        output.ToString().Should().Contain($"'{ws.Name}' restored at");
    }

    [Fact]
    public async Task InvokeAsync_GitHubLinked_PrintsGitHubLine()
    {
        var ws = MakeWorkspace(gitHubRepo: "owner/repo");
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: true, GitHubLinked: true));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--path", @"C:\test"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        output.ToString().Should().Contain("GitHub: owner/repo");
    }

    [Fact]
    public async Task InvokeAsync_NoGitHubDetectFlag_SendsDetectGitHubFalse()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: true, GitHubLinked: false));

        var cmd = new InitWorkspaceCliCommand(mediator);
        await cmd.InvokeAsync(["--path", @"C:\test", "--no-github-detect"]);

        await mediator.Received(1).Send(
            Arg.Is<InitWorkspaceCommand>(c => c.DetectGitHub == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NeedsArchivedAction_Restore_SendsRestoreAction()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(
                Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == null),
                Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: false, GitHubLinked: false, NeedsArchivedAction: true));
        mediator.Send(
                Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == InitWorkspaceArchivedAction.Restore),
                Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: false, GitHubLinked: false, Restored: true));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        Console.SetIn(new StringReader("restore\n"));
        try
        {
            await cmd.InvokeAsync(["--path", @"C:\test"]);
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        await mediator.Received(1).Send(
            Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == InitWorkspaceArchivedAction.Restore),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NeedsArchivedAction_Fresh_SendsFreshAction()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(
                Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == null),
                Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: false, GitHubLinked: false, NeedsArchivedAction: true));
        mediator.Send(
                Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == InitWorkspaceArchivedAction.Fresh),
                Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: true, GitHubLinked: false));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        Console.SetIn(new StringReader("fresh\n"));
        try
        {
            await cmd.InvokeAsync(["--path", @"C:\test"]);
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        await mediator.Received(1).Send(
            Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == InitWorkspaceArchivedAction.Fresh),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NeedsArchivedAction_InvalidInput_PrintsCancelledAndNoSecondSend()
    {
        var ws = MakeWorkspace();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(
                Arg.Is<InitWorkspaceCommand>(c => c.ArchivedAction == null),
                Arg.Any<CancellationToken>())
            .Returns(new InitWorkspaceResult(ws, Created: false, GitHubLinked: false, NeedsArchivedAction: true));

        var cmd = new InitWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetIn(new StringReader("nope\n"));
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--path", @"C:\test"]);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }

        output.ToString().Should().Contain("Cancelled.");
        await mediator.Received(1).Send(Arg.Any<InitWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }
}

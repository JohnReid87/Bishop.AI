using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.PushLane;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.Push;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Push;

[Collection("ConsoleTests")]
public sealed class PushCardCliCommandTests
{
    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test", GitHubRepo = "owner/repo" };

    private static (IMediator mediator, PushCardCliCommand cmd) BuildSingleCard(Workspace ws, Card card)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);
        var cardResolver = new CardResolver(mediator);
        return (mediator, new PushCardCliCommand(mediator, cardResolver));
    }

    private static (IMediator mediator, PushCardCliCommand cmd) BuildLane(Workspace ws, PushLaneResult result)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<PushLaneCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);
        var cardResolver = new CardResolver(mediator);
        return (mediator, new PushCardCliCommand(mediator, cardResolver));
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsPushCardCommand()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do", GitHubIssueNumber = 42 };
        var (mediator, cmd) = BuildSingleCard(ws, card);

        var exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CardIdAndLaneBothSupplied_SetsExitCode1AndWritesErrorToStderr()
    {
        var mediator = Substitute.For<IMediator>();
        var cardResolver = new CardResolver(mediator);
        var cmd = new PushCardCliCommand(mediator, cardResolver);

        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        var originalExitCode = Environment.ExitCode;
        Console.SetError(errorOutput);
        Environment.ExitCode = 0;
        try
        {
            await cmd.InvokeAsync(["#1", "--lane", "To Do", "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Console.SetError(originalErr);
            Environment.ExitCode = originalExitCode;
        }

        errorOutput.ToString().Should().Contain("mutually exclusive");
        await mediator.DidNotReceive().Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(Arg.Any<PushLaneCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NeitherCardIdNorLane_SetsExitCode1AndWritesErrorToStderr()
    {
        var mediator = Substitute.For<IMediator>();
        var cardResolver = new CardResolver(mediator);
        var cmd = new PushCardCliCommand(mediator, cardResolver);

        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        var originalExitCode = Environment.ExitCode;
        Console.SetError(errorOutput);
        Environment.ExitCode = 0;
        try
        {
            await cmd.InvokeAsync(["--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Console.SetError(originalErr);
            Environment.ExitCode = originalExitCode;
        }

        errorOutput.ToString().Should().Contain("Specify either a card-id or --lane");
        await mediator.DidNotReceive().Send(Arg.Any<PushCardCommand>(), Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(Arg.Any<PushLaneCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_LanePath_HappyPath_PrintsSummaryAndPerCardLines()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do", GitHubIssueNumber = 42 };
        var result = new PushLaneResult(Pushed: [card], SkippedAlreadyLinked: 2, Failed: []);
        var (_, cmd) = BuildLane(ws, result);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--lane", "To Do", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("pushed 1, skipped 2 (already linked), failed 0.");
        output.ToString().Should().Contain("pushed  #1  Test Card  https://github.com/owner/repo/issues/42");
    }

    [Fact]
    public async Task InvokeAsync_LanePath_DryRun_PrintsDryRunPrefixAndWouldPushLines()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do", GitHubIssueNumber = 42 };
        var result = new PushLaneResult(Pushed: [card], SkippedAlreadyLinked: 0, Failed: []);
        var (_, cmd) = BuildLane(ws, result);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--lane", "To Do", "--dry-run", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("[dry-run] pushed 1, skipped 0 (already linked), failed 0.");
        output.ToString().Should().Contain("would push  #1  Test Card");
        output.ToString().Should().NotContain("https://github.com");
    }

    [Fact]
    public async Task InvokeAsync_LanePath_FailuresPresent_SetsExitCode1AndPrintsFailureLines()
    {
        var ws = DefaultWorkspace();
        var result = new PushLaneResult(Pushed: [], SkippedAlreadyLinked: 0, Failed: [new PushLaneFailure(CardNumber: 5, Error: "gh error")]);
        var (_, cmd) = BuildLane(ws, result);

        var output = new StringWriter();
        var originalOut = Console.Out;
        var originalExitCode = Environment.ExitCode;
        Console.SetOut(output);
        Environment.ExitCode = 0;
        try
        {
            await cmd.InvokeAsync(["--lane", "To Do", "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.ExitCode = originalExitCode;
        }

        output.ToString().Should().Contain("failed  #5  gh error");
    }
}

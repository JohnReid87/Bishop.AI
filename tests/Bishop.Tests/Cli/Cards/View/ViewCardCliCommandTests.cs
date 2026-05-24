using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.View;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;
using System.Text.Json;

namespace Bishop.Tests.Cli.Cards.View;

[Collection("ConsoleTests")]
public sealed class ViewCardCliCommandTests
{
    private static (IMediator mediator, ViewCardCliCommand cmd) Build(Workspace ws, Card? card)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        if (card is not null)
        {
            mediator.Send(Arg.Any<GetCardQuery>(), Arg.Any<CancellationToken>())
                .Returns(card);
            mediator.Send(Arg.Any<GetCardCommitQuery>(), Arg.Any<CancellationToken>())
                .Returns(new GetCardCommitResult.NotFound());
        }
        var cardResolver = new CardResolver(mediator);
        return (mediator, new ViewCardCliCommand(mediator, cardResolver));
    }

    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };

    [Fact]
    public async Task InvokeAsync_HappyPath_PrintsTitleAndLane()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        var text = output.ToString();
        text.Should().Contain("Test Card");
        text.Should().Contain("Lane: To Do");
    }

    [Fact]
    public async Task InvokeAsync_ClosedCard_PrintsStatusClosed()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Done",
            IsClosed = true
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Status: closed");
    }

    [Fact]
    public async Task InvokeAsync_CardWithTag_PrintsTagLine()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "To Do",
            TagName = "feature"
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Tag: feature");
    }

    [Fact]
    public async Task InvokeAsync_NonZeroClaudeTotals_PrintsClaudeSummaryLine()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "To Do",
            ClaudeRunCount = 2, TotalInputTokens = 3000, TotalOutputTokens = 1500
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Claude:").And.Contain("run");
    }

    [Fact]
    public async Task InvokeAsync_CardWithDescription_PrintsDescriptionText()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "To Do",
            Description = "Detailed description here."
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Detailed description here.");
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsValidJsonWithCardFields()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "To Do",
            TagName = "feature"
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        var root = doc.RootElement;
        root.GetProperty("number").GetInt32().Should().Be(1);
        root.GetProperty("title").GetString().Should().Be("Test Card");
        root.GetProperty("laneName").GetString().Should().Be("To Do");
        root.GetProperty("tag").GetString().Should().Be("feature");
        root.TryGetProperty("commit", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CardNotFound_ExitsNonZero()
    {
        var ws = DefaultWorkspace();
        var (_, cmd) = Build(ws, null);

        var exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]);

        exitCode.Should().NotBe(0);
    }
}

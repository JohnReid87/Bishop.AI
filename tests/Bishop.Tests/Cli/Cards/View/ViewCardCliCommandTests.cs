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
    private static (IMediator mediator, ViewCardCliCommand cmd) Build(Workspace ws, Card? card, GetCardCommitResult? commitResult = null)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        if (card is not null)
        {
            mediator.Send(Arg.Is<GetCardByNumberQuery>(q => q.Number == card.Number && q.WorkspaceId == ws.Id), Arg.Any<CancellationToken>())
                .Returns(card);
            mediator.Send(Arg.Is<GetCardQuery>(q => q.CardId == card.Id), Arg.Any<CancellationToken>())
                .Returns(card);
            mediator.Send(Arg.Is<GetCardCommitQuery>(q => q.CardNumber == card.Number && q.WorkspacePath == ws.Path), Arg.Any<CancellationToken>())
                .Returns(commitResult ?? new GetCardCommitResult.NotFound());
        }
        else
        {
            mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
                .Returns((Card?)null);
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
        var lines = output.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("Test Card");
        lines[1].Should().Be("Lane: To Do");
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

    [Fact]
    public async Task InvokeAsync_GetCardQueryReturnsNull_ExitsNonZero()
    {
        var ws = DefaultWorkspace();
        var cardId = Guid.NewGuid();
        var resolved = new Card { Id = cardId, WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Is<GetCardByNumberQuery>(q => q.Number == 1 && q.WorkspaceId == ws.Id), Arg.Any<CancellationToken>())
            .Returns(resolved);
        mediator.Send(Arg.Is<GetCardQuery>(q => q.CardId == cardId), Arg.Any<CancellationToken>())
            .Returns((Card?)null);

        var cardResolver = new CardResolver(mediator);
        var cmd = new ViewCardCliCommand(mediator, cardResolver);

        var exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]);

        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_FoundCommitWithNullGitHubRepo_CommitUrlIsNull()
    {
        var ws = DefaultWorkspace(); // GitHubRepo is null
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "To Do"
        };

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Is<GetCardByNumberQuery>(q => q.Number == card.Number && q.WorkspaceId == ws.Id), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Is<GetCardQuery>(q => q.CardId == card.Id), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Is<GetCardCommitQuery>(q => q.CardNumber == card.Number && q.WorkspacePath == ws.Path), Arg.Any<CancellationToken>())
            .Returns(new GetCardCommitResult.Found(
                new CommitInfo("abc1234", "abc1234def5678901234567890abcdef", "Fix bug", "", DateTimeOffset.UtcNow, false)));

        var cardResolver = new CardResolver(mediator);
        var cmd = new ViewCardCliCommand(mediator, cardResolver);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        var commitProp = doc.RootElement.GetProperty("commit");
        commitProp.ValueKind.Should().NotBe(JsonValueKind.Null);
        commitProp.GetProperty("url").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_FoundCommitWithGitHubRepo_CommitUrlIsPopulated()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test", GitHubRepo = "testowner/testrepo" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var fullHash = "abc1234def5678901234567890abcdef";
        var commitResult = new GetCardCommitResult.Found(
            new CommitInfo("abc1234", fullHash, "Fix bug", "", DateTimeOffset.UtcNow, false));
        var (_, cmd) = Build(ws, card, commitResult);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["#1", "--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        var commitProp = doc.RootElement.GetProperty("commit");
        commitProp.ValueKind.Should().NotBe(JsonValueKind.Null);
        commitProp.GetProperty("url").GetString()
            .Should().Be($"https://github.com/testowner/testrepo/commit/{fullHash}");
    }

    [Fact]
    public async Task InvokeAsync_ZeroClaudeTotals_OmitsClaudeSummaryLine()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "To Do",
            ClaudeRunCount = 0, TotalInputTokens = 0, TotalOutputTokens = 0
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["#1", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().NotContain("Claude:");
    }
}

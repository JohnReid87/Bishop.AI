using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.List;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;
using System.Text.Json;

namespace Bishop.Tests.Cli.Cards.List;

[Collection("ConsoleTests")]
public sealed class ListCardsCliCommandTests
{
    private static (IMediator mediator, ListCardsCliCommand cmd) Build(
        Workspace ws, IReadOnlyList<Card> cards, IReadOnlyList<LaneInfo>? lanes = null)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(cards);
        mediator.Send(Arg.Any<ListLanesByWorkspaceQuery>(), Arg.Any<CancellationToken>())
            .Returns(lanes ?? (IReadOnlyList<LaneInfo>)[]);
        return (mediator, new ListCardsCliCommand(mediator));
    }

    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };

    [Fact]
    public async Task InvokeAsync_EmptyCards_ExitsZeroAndOutputsNothing()
    {
        var ws = DefaultWorkspace();
        var (_, cmd) = Build(ws, []);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        output.ToString().Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_PopulatedCards_PrintsLaneHeadersAndCardRows()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Alpha", LaneName = "To Do", Position = 0 },
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 2, Title = "Beta",  LaneName = "Doing", Position = 0 },
        ];
        var lanes = (IReadOnlyList<LaneInfo>)[new LaneInfo("To Do", 1), new LaneInfo("Doing", 2)];
        var (_, cmd) = Build(ws, cards, lanes);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("[To Do]");
        text.Should().Contain("Alpha");
        text.Should().Contain("[Doing]");
        text.Should().Contain("Beta");
        // To Do section should appear before Doing section
        text.IndexOf("[To Do]", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("[Doing]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_ClosedCard_PrintsClosedMarker()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Old", LaneName = "Done", Position = 0, IsClosed = true },
        ];
        var (_, cmd) = Build(ws, cards);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("[closed]");
    }

    [Fact]
    public async Task InvokeAsync_CardWithTag_PrintsTagSuffix()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Task", LaneName = "To Do", Position = 0, TagName = "feature" },
        ];
        var (_, cmd) = Build(ws, cards);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("[feature]");
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsValidJsonArray()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Alpha", LaneName = "To Do", Position = 0 },
        ];
        var (_, cmd) = Build(ws, cards);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);
        var item = doc.RootElement[0];
        item.GetProperty("number").GetInt32().Should().Be(1);
        item.GetProperty("title").GetString().Should().Be("Alpha");
        item.GetProperty("laneName").GetString().Should().Be("To Do");
        item.GetProperty("isStarred").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_StarredCard_ReportsIsStarredTrue()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Alpha", LaneName = "To Do", Position = 0, IsStarred = true },
        ];
        var (_, cmd) = Build(ws, cards);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        using var doc = JsonDocument.Parse(output.ToString());
        doc.RootElement[0].GetProperty("isStarred").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_TagAndLaneFilter_OutputsMatchingCards()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 5, Title = "Filtered card", LaneName = "To Do", Position = 0, TagName = "feature" },
        ];
        var lanes = (IReadOnlyList<LaneInfo>)[new LaneInfo("To Do", 1)];
        var (_, cmd) = Build(ws, cards, lanes);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--tag", "feature", "--lane", "To Do"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("Filtered card");
        text.Should().Contain("[feature]");
        text.Should().Contain("[To Do]");
    }

    [Fact]
    public async Task InvokeAsync_TagFilterOnly_OutputsMatchingCard()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 3, Title = "Tagged card", LaneName = "To Do", Position = 0, TagName = "feature" },
        ];
        var (_, cmd) = Build(ws, cards);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--tag", "feature"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("Tagged card");
        text.Should().Contain("[feature]");
    }

    [Fact]
    public async Task InvokeAsync_LaneFilterOnly_OutputsMatchingCard()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 4, Title = "Lane card", LaneName = "Doing", Position = 0 },
        ];
        var lanes = (IReadOnlyList<LaneInfo>)[new LaneInfo("Doing", 2)];
        var (_, cmd) = Build(ws, cards, lanes);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--lane", "Doing"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("[Doing]");
        text.Should().Contain("Lane card");
    }

    [Fact]
    public async Task InvokeAsync_UnknownLane_SortsAfterKnownLanes()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Known", LaneName = "To Do", Position = 0 },
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 2, Title = "Orphan", LaneName = "Archive", Position = 0 },
        ];
        var lanes = (IReadOnlyList<LaneInfo>)[new LaneInfo("To Do", 1)];
        var (_, cmd) = Build(ws, cards, lanes);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("[To Do]");
        text.Should().Contain("[Archive]");
        text.IndexOf("[To Do]", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("[Archive]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_EmptyStringLaneName_SortsAfterKnownLanes()
    {
        var ws = DefaultWorkspace();
        var cards = (IReadOnlyList<Card>)[
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Known", LaneName = "To Do", Position = 0 },
            new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 2, Title = "Missing", LaneName = "", Position = 0 },
        ];
        var lanes = (IReadOnlyList<LaneInfo>)[new LaneInfo("To Do", 1)];
        var (_, cmd) = Build(ws, cards, lanes);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("[To Do]");
        text.Should().Contain("Missing");
        text.IndexOf("[To Do]", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("Missing", StringComparison.Ordinal));
    }
}

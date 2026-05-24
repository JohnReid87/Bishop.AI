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
    public async Task InvokeAsync_EmptyCards_ExitsZeroAndSendsListCardsByWorkspaceQuery()
    {
        var ws = DefaultWorkspace();
        var (mediator, cmd) = Build(ws, []);

        var exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListCardsByWorkspaceQuery>(), Arg.Any<CancellationToken>());
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
    }

    [Fact]
    public async Task InvokeAsync_TagAndLaneFilter_SendsQueryWithBothFilters()
    {
        var ws = DefaultWorkspace();
        var (mediator, cmd) = Build(ws, []);

        await cmd.InvokeAsync(["--workspace", "test-ws", "--tag", "feature", "--lane", "To Do"]);

        await mediator.Received(1).Send(
            Arg.Is<ListCardsByWorkspaceQuery>(q => q.TagName == "feature" && q.LaneName == "To Do"),
            Arg.Any<CancellationToken>());
    }
}

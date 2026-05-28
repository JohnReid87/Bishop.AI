using Bishop.App.Cards.ClaimCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.Claim;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;
using System.Text.Json;

namespace Bishop.Tests.Cli.Cards.Claim;

[Collection("ConsoleTests")]
public sealed class ClaimCardCliCommandTests
{
    private static (IMediator mediator, ClaimCardCliCommand cmd) Build(
        Workspace ws, Card? card)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ClaimCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);
        return (mediator, new ClaimCardCliCommand(mediator));
    }

    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };

    [Fact]
    public async Task InvokeAsync_HappyPath_PrintsClaimConfirmationLine()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing"
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Claimed #1 — 'Test Card' [To Do] → [Doing]");
    }

    [Fact]
    public async Task InvokeAsync_CardHasTag_PrintsTagLine()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing",
            TagName = "feature"
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Tag: feature");
    }

    [Fact]
    public async Task InvokeAsync_CardHasDescription_PrintsDescriptionInOutput()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing",
            Description = "Do the thing."
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Do the thing.");
    }

    [Fact]
    public async Task InvokeAsync_CardNotFound_WritesErrorToStderr()
    {
        var ws = DefaultWorkspace();
        var (_, cmd) = Build(ws, null);

        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetError(originalErr); }

        errorOutput.ToString().Should().Contain("Lane 'To Do' is empty");
    }

    [Fact]
    public async Task InvokeAsync_TagSupplied_CardNotFound_WritesTagSpecificErrorToStderr()
    {
        var ws = DefaultWorkspace();
        var (_, cmd) = Build(ws, null);

        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--tag", "bug"]); }
        finally { Console.SetError(originalErr); }

        errorOutput.ToString().Should().Contain("No card tagged 'bug' in 'To Do'.");
    }

    [Fact]
    public async Task InvokeAsync_CardDescriptionNull_OmitsDescriptionSectionFromOutput()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing"
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Trim().Should().Be("Claimed #1 — 'Test Card' [To Do] → [Doing]");
    }

    [Fact]
    public async Task InvokeAsync_CardDescriptionEmpty_OmitsDescriptionSectionFromOutput()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing",
            Description = ""
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Trim().Should().Be("Claimed #1 — 'Test Card' [To Do] → [Doing]");
    }

    [Fact]
    public async Task InvokeAsync_TagFilter_SendsCommandWithTagName()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing",
            TagName = "feature"
        };
        var (mediator, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--tag", "feature"]); }
        finally { Console.SetOut(originalOut); }

        await mediator.Received(1).Send(
            Arg.Is<ClaimCardCommand>(c => c.TagName == "feature"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_LaneOption_SendsCommandWithSourceLaneName()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing"
        };
        var (mediator, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--lane", "Backlog"]); }
        finally { Console.SetOut(originalOut); }

        await mediator.Received(1).Send(
            Arg.Is<ClaimCardCommand>(c => c.SourceLaneName == "Backlog"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsValidJsonWithCardFields()
    {
        var ws = DefaultWorkspace();
        var card = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Number = 1, Title = "Test Card", LaneName = "Doing"
        };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        var root = doc.RootElement;
        root.GetProperty("number").GetInt32().Should().Be(1);
        root.GetProperty("title").GetString().Should().Be("Test Card");
        root.GetProperty("laneName").GetString().Should().Be("Doing");
        root.TryGetProperty("id", out _).Should().BeTrue();
    }
}

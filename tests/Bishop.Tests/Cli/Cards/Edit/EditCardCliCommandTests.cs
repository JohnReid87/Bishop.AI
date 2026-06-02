using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli;
using Bishop.Cli.Cards.Edit;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Edit;

[Collection("ConsoleTests")]
public sealed class EditCardCliCommandTests
{
    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };

    private static (IMediator mediator, EditCardCliCommand cmd) Build(Workspace ws, Card card)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<GetCardByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(card);
        mediator.Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);
        var cardResolver = new CardResolver(mediator);
        return (mediator, new EditCardCliCommand(mediator, cardResolver));
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsUpdateCardCommandWithCorrectParameters()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Updated Title", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var exitCode = await cmd.InvokeAsync(["#1", "--title", "Updated Title", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<UpdateCardCommand>(c =>
                c.CardId == card.Id &&
                c.Title == "Updated Title" &&
                c.Description == null &&
                c.UpdateTag == false &&
                c.TagName == null &&
                c.ToLaneName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_HappyPath_PrintsUpdatedCardMessage()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Updated Title", LaneName = "To Do" };
        var (_, cmd) = Build(ws, card);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["#1", "--title", "Updated Title", "--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        output.ToString().Should().Contain("Updated card #1").And.Contain("Updated Title");
    }

    [Fact]
    public async Task InvokeAsync_WithDescriptionOption_SendsUpdateCardCommandWithDescription()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var exitCode = await cmd.InvokeAsync(["#1", "--description", "Inline description", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<UpdateCardCommand>(c => c.Description == "Inline description"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithTagOption_SendsUpdateCardCommandWithUpdateTagTrueAndTagName()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var exitCode = await cmd.InvokeAsync(["#1", "--tag", "feature", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<UpdateCardCommand>(c => c.UpdateTag == true && c.TagName == "feature"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithToLaneOption_SendsUpdateCardCommandWithToLaneName()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var exitCode = await cmd.InvokeAsync(["#1", "--to-lane", "Doing", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<UpdateCardCommand>(c => c.ToLaneName == "Doing"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DescriptionAndDescriptionFileTogether_SetsExitCode1AndDoesNotSendUpdateCardCommand()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var originalExitCode = Environment.ExitCode;
        Environment.ExitCode = 0;
        try
        {
            await cmd.InvokeAsync(["#1", "--description", "text", "--description-file", "file.txt", "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DescriptionAndAppendDescriptionFileTogether_SetsExitCode1AndDoesNotSendUpdateCardCommand()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var originalExitCode = Environment.ExitCode;
        Environment.ExitCode = 0;
        try
        {
            await cmd.InvokeAsync(["#1", "--description", "text", "--append-description-file", "file.txt", "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DescriptionFileAndAppendDescriptionFileTogether_SetsExitCode1AndDoesNotSendUpdateCardCommand()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var originalExitCode = Environment.ExitCode;
        Environment.ExitCode = 0;
        try
        {
            await cmd.InvokeAsync(["#1", "--description-file", "file.txt", "--append-description-file", "other.txt", "--workspace", "test-ws"]);
            Environment.ExitCode.Should().Be(1);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }

        await mediator.DidNotReceive().Send(Arg.Any<UpdateCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DescriptionFile_ReadsContentFromFileAndSendsAsDescription()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, "File description content");
            var exitCode = await cmd.InvokeAsync(["#1", "--description-file", tmpFile, "--workspace", "test-ws"]);

            exitCode.Should().Be(0);
            await mediator.Received(1).Send(
                Arg.Is<UpdateCardCommand>(c => c.Description == "File description content"),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_AppendDescriptionFile_ReadsContentFromFileAndSendsAsAppendDescription()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (mediator, cmd) = Build(ws, card);

        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, "Appended content");
            var exitCode = await cmd.InvokeAsync(["#1", "--append-description-file", tmpFile, "--workspace", "test-ws"]);

            exitCode.Should().Be(0);
            await mediator.Received(1).Send(
                Arg.Is<UpdateCardCommand>(c => c.AppendDescription == "Appended content"),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_DescriptionFileMissing_ExitsNonZero()
    {
        var ws = DefaultWorkspace();
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var (_, cmd) = Build(ws, card);

        var exitCode = await cmd.InvokeAsync(["#1", "--description-file", @"C:\nonexistent\missing_file_12345.txt", "--workspace", "test-ws"]);

        exitCode.Should().NotBe(0);
    }
}

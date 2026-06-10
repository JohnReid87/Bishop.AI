using Bishop.App.Cards.AddCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.Create;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Cards.Create;

public sealed class CreateCardCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsAddCardCommand()
    {
        var ws = new Workspace { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };
        var card = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Number = 1, Title = "Test Card", LaneName = "To Do" };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(card);

        var cmd = new CreateCardCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--lane", "To Do", "--title", "Test Card", "--workspace", "test-ws"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveDescriptionAsync_RedirectedStdin_NoFlags_ReadsStdin()
    {
        var stdin = new StringReader("piped body");

        var desc = await CreateCardCliCommand.ResolveDescriptionAsync(null, null, isInputRedirected: true, stdin);

        desc.Should().Be("piped body");
    }

    [Fact]
    public async Task ResolveDescriptionAsync_NotRedirected_NoFlags_ReturnsEmpty()
    {
        var stdin = new StringReader("ignored");

        var desc = await CreateCardCliCommand.ResolveDescriptionAsync(null, null, isInputRedirected: false, stdin);

        desc.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveDescriptionAsync_DescriptionFlag_WinsOverRedirectedStdin()
    {
        var stdin = new StringReader("piped body");

        var desc = await CreateCardCliCommand.ResolveDescriptionAsync(null, "explicit", isInputRedirected: true, stdin);

        desc.Should().Be("explicit");
    }

    [Fact]
    public async Task ResolveDescriptionAsync_DescriptionFileDash_ReadsStdin()
    {
        var stdin = new StringReader("sentinel body");

        var desc = await CreateCardCliCommand.ResolveDescriptionAsync("-", "explicit", isInputRedirected: false, stdin);

        desc.Should().Be("sentinel body");
    }
}

using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.RemoveWorkspace;
using Bishop.Cli.Workspaces.Remove;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.Remove;

[Collection("EnvVar")]
public sealed class RemoveWorkspaceCliCommandTests
{
    private static Workspace MakeWorkspace(string name = "test-ws") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Path = Directory.GetCurrentDirectory()
    };

    private static IMediator BuildMediator(Workspace workspace)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[workspace]);
        return mediator;
    }

    [Fact]
    public async Task DryRun_ExitsZeroWithoutSendingRemoveCommand()
    {
        var ws = MakeWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new RemoveWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--yes", "--dry-run", "--workspace", ws.Name]);

        exitCode.Should().Be(0);
        await mediator.DidNotReceive().Send(Arg.Any<RemoveWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DryRun_PrintsDryRunMessage()
    {
        var ws = MakeWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new RemoveWorkspaceCliCommand(mediator);
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--yes", "--dry-run", "--workspace", ws.Name]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("[dry-run]");
    }

    [Fact]
    public async Task YesFlag_ExitsZeroAndSendsRemoveCommand()
    {
        var ws = MakeWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new RemoveWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--yes", "--workspace", ws.Name]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RemoveWorkspaceCommand>(c => c.Id == ws.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task YesFlag_PrintsWorkspaceRemovedMessage()
    {
        var ws = MakeWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new RemoveWorkspaceCliCommand(mediator);
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--yes", "--workspace", ws.Name]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("removed");
    }

    [Fact]
    public async Task ConfirmationDeclined_DoesNotSendRemoveCommand()
    {
        var ws = MakeWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new RemoveWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        Console.SetIn(new StringReader("n\n"));
        try
        {
            await cmd.InvokeAsync(["--workspace", ws.Name]);
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        await mediator.DidNotReceive().Send(Arg.Any<RemoveWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmationAccepted_SendsRemoveCommand()
    {
        var ws = MakeWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new RemoveWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        Console.SetIn(new StringReader("y\n"));
        try
        {
            await cmd.InvokeAsync(["--workspace", ws.Name]);
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        await mediator.Received(1).Send(
            Arg.Is<RemoveWorkspaceCommand>(c => c.Id == ws.Id),
            Arg.Any<CancellationToken>());
    }
}

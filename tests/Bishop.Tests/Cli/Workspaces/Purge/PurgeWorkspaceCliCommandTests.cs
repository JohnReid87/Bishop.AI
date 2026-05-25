using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.PurgeWorkspace;
using Bishop.Cli.Workspaces.Purge;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.Purge;

[Collection("EnvVar")]
public sealed class PurgeWorkspaceCliCommandTests
{
    private const string WorkspacePath = @"C:\bishop-purge-test";

    private static Workspace MakeRemovedWorkspace(string name = "purge-ws", string? path = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Path = path ?? WorkspacePath,
        IsRemoved = true,
        RemovedAt = DateTimeOffset.UtcNow
    };

    private static IMediator BuildMediator(params Workspace[] removed)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[.. removed]);
        return mediator;
    }

    [Fact]
    public async Task NeitherPathNorName_WritesErrorAndDoesNotSendPurgeCommand()
    {
        var mediator = Substitute.For<IMediator>();
        var cmd = new PurgeWorkspaceCliCommand(mediator);
        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync([]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        exitCode.Should().Be(0);
        errorOutput.ToString().Should().Contain("error:");
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Path_HappyPath_YesFlag_SendsPurgeCommand()
    {
        var ws = MakeRemovedWorkspace(path: WorkspacePath);
        var mediator = BuildMediator(ws);
        var cmd = new PurgeWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--path", WorkspacePath, "--yes"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<PurgeWorkspaceCommand>(c => c.Id == ws.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Path_HappyPath_DeletesBishopDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bishop-purge-" + Guid.NewGuid().ToString("N")[..8]);
        var bishopDir = Path.Combine(tempDir, ".bishop");
        Directory.CreateDirectory(bishopDir);
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);

            await cmd.InvokeAsync(["--path", tempDir, "--yes"]);

            Directory.Exists(bishopDir).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Path_NotFound_DoesNotSendPurgeCommand()
    {
        var mediator = BuildMediator();
        var cmd = new PurgeWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--path", WorkspacePath, "--yes"]);

        exitCode.Should().Be(1);
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Name_HappyPath_YesFlag_SendsPurgeCommand()
    {
        var ws = MakeRemovedWorkspace(name: "old-ws");
        var mediator = BuildMediator(ws);
        var cmd = new PurgeWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--name", "old-ws", "--yes"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<PurgeWorkspaceCommand>(c => c.Id == ws.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Name_NotFound_ExitCode1AndDoesNotSendPurgeCommand()
    {
        var mediator = BuildMediator();
        var cmd = new PurgeWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--name", "nonexistent-ws", "--yes"]);

        exitCode.Should().Be(1);
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Name_MultipleMatches_WritesErrorAndDoesNotSendPurgeCommand()
    {
        var ws1 = MakeRemovedWorkspace(name: "dup-ws", path: @"C:\dup-path-1");
        var ws2 = MakeRemovedWorkspace(name: "dup-ws", path: @"C:\dup-path-2");
        var mediator = BuildMediator(ws1, ws2);
        var cmd = new PurgeWorkspaceCliCommand(mediator);
        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--name", "dup-ws", "--yes"]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        exitCode.Should().Be(0);
        errorOutput.ToString().Should().Contain("error:");
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DryRun_DoesNotSendPurgeCommand()
    {
        var ws = MakeRemovedWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new PurgeWorkspaceCliCommand(mediator);

        var exitCode = await cmd.InvokeAsync(["--name", ws.Name, "--dry-run", "--yes"]);

        exitCode.Should().Be(0);
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmationAccepted_SendsPurgeCommand()
    {
        var ws = MakeRemovedWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new PurgeWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        Console.SetIn(new StringReader("y\n"));
        try
        {
            await cmd.InvokeAsync(["--name", ws.Name]);
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        await mediator.Received(1).Send(
            Arg.Is<PurgeWorkspaceCommand>(c => c.Id == ws.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmationDeclined_DoesNotSendPurgeCommand()
    {
        var ws = MakeRemovedWorkspace();
        var mediator = BuildMediator(ws);
        var cmd = new PurgeWorkspaceCliCommand(mediator);
        var originalIn = Console.In;
        Console.SetIn(new StringReader("n\n"));
        try
        {
            await cmd.InvokeAsync(["--name", ws.Name]);
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }
}

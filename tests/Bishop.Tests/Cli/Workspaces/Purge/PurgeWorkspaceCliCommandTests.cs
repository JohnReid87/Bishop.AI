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

    private static string CreateTempWorkspaceWithBishopDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bishop-purge-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(dir, ".bishop"));
        return dir;
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

        exitCode.Should().Be(1);
        errorOutput.ToString().Should().Contain("error:");
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Path_HappyPath_YesFlag_PurgesWorkspaceAndDeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);
            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            int exitCode;
            try
            {
                exitCode = await cmd.InvokeAsync(["--path", tempDir, "--yes"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            exitCode.Should().Be(0);
            var outputStr = output.ToString();
            outputStr.Should().Contain(ws.Name);
            outputStr.Should().Contain("purged");
            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Path_HappyPath_DeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);

            await cmd.InvokeAsync(["--path", tempDir, "--yes"]);

            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
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
        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--path", WorkspacePath, "--yes"]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        exitCode.Should().Be(1);
        var errStr = errorOutput.ToString();
        errStr.Should().Contain(WorkspacePath);
        errStr.Should().Contain("archived");
        await mediator.DidNotReceive().Send(Arg.Any<PurgeWorkspaceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Name_HappyPath_YesFlag_PurgesWorkspaceAndDeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(name: "old-ws", path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);
            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            int exitCode;
            try
            {
                exitCode = await cmd.InvokeAsync(["--name", "old-ws", "--yes"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            exitCode.Should().Be(0);
            var outputStr = output.ToString();
            outputStr.Should().Contain(ws.Name);
            outputStr.Should().Contain("purged");
            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Name_NotFound_ExitCode1AndDoesNotSendPurgeCommand()
    {
        var mediator = BuildMediator();
        var cmd = new PurgeWorkspaceCliCommand(mediator);
        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--name", "nonexistent-ws", "--yes"]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        exitCode.Should().Be(1);
        var errStr = errorOutput.ToString();
        errStr.Should().Contain("nonexistent-ws");
        errStr.Should().Contain("archived");
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

        exitCode.Should().Be(1);
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
    public async Task ConfirmationAccepted_DeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
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

            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
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

    [Fact]
    public async Task Path_ForwardSlashSeparator_FindsWorkspaceAndDeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);
            var forwardSlashPath = tempDir.Replace('\\', '/');

            var exitCode = await cmd.InvokeAsync(["--path", forwardSlashPath, "--yes"]);

            exitCode.Should().Be(0);
            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Path_DifferentCasing_FindsWorkspaceAndDeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);

            var exitCode = await cmd.InvokeAsync(["--path", tempDir.ToUpperInvariant(), "--yes"]);

            exitCode.Should().Be(0);
            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Path_NoBishopDirectory_ExitCode0AndOmitsBishopDirFromOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bishop-purge-nodotbishop-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);
            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            int exitCode;
            try
            {
                exitCode = await cmd.InvokeAsync(["--path", tempDir, "--yes"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            exitCode.Should().Be(0);
            output.ToString().Should().NotContain(".bishop");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ConfirmationAccepted_UppercaseYes_DeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);
            var originalIn = Console.In;
            Console.SetIn(new StringReader("YES\n"));
            try
            {
                await cmd.InvokeAsync(["--name", ws.Name]);
            }
            finally
            {
                Console.SetIn(originalIn);
            }

            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ConfirmationAccepted_PaddedY_DeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);
            var originalIn = Console.In;
            Console.SetIn(new StringReader(" y \n"));
            try
            {
                await cmd.InvokeAsync(["--name", ws.Name]);
            }
            finally
            {
                Console.SetIn(originalIn);
            }

            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Name_CaseInsensitive_FindsWorkspaceAndDeletesBishopDirectory()
    {
        var tempDir = CreateTempWorkspaceWithBishopDir();
        try
        {
            var ws = MakeRemovedWorkspace(name: "bishop", path: tempDir);
            var mediator = BuildMediator(ws);
            var cmd = new PurgeWorkspaceCliCommand(mediator);

            var exitCode = await cmd.InvokeAsync(["--name", "Bishop", "--yes"]);

            exitCode.Should().Be(0);
            Directory.Exists(Path.Combine(tempDir, ".bishop")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PathAndName_BothSupplied_PathWorkspaceIsPurged()
    {
        var tempDirByPath = CreateTempWorkspaceWithBishopDir();
        var tempDirByName = CreateTempWorkspaceWithBishopDir();
        try
        {
            var wsByPath = MakeRemovedWorkspace(name: "path-ws", path: tempDirByPath);
            var wsByName = MakeRemovedWorkspace(name: "name-ws", path: tempDirByName);
            var mediator = BuildMediator(wsByPath, wsByName);
            var cmd = new PurgeWorkspaceCliCommand(mediator);

            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            int exitCode;
            try
            {
                exitCode = await cmd.InvokeAsync(["--path", tempDirByPath, "--name", "name-ws", "--yes"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            exitCode.Should().Be(0);
            Directory.Exists(Path.Combine(tempDirByPath, ".bishop")).Should().BeFalse("path workspace .bishop directory should be deleted");
            Directory.Exists(Path.Combine(tempDirByName, ".bishop")).Should().BeTrue("name workspace .bishop directory should not be deleted");
        }
        finally
        {
            if (Directory.Exists(tempDirByPath)) Directory.Delete(tempDirByPath, recursive: true);
            if (Directory.Exists(tempDirByName)) Directory.Delete(tempDirByName, recursive: true);
        }
    }

    [Fact]
    public async Task Name_ActiveWorkspaceInList_NotPurged()
    {
        var active = new Workspace { Id = Guid.NewGuid(), Name = "active-ws", Path = @"C:\active", IsRemoved = false };
        var removed = MakeRemovedWorkspace(name: "removed-ws");

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[active, removed]);

        var cmd = new PurgeWorkspaceCliCommand(mediator);
        var errorOutput = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(errorOutput);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--name", "active-ws", "--yes"]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        exitCode.Should().NotBe(0);
        await mediator.DidNotReceive().Send(
            Arg.Is<PurgeWorkspaceCommand>(c => c.Id == active.Id),
            Arg.Any<CancellationToken>());
    }
}

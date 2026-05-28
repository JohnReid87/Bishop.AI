using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Workspaces.List;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Workspaces.List;

[Collection("ConsoleTests")]
public sealed class ListWorkspacesCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsListWorkspacesQuery()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new ListWorkspacesCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NoFlags_SendsQueryWithIncludeRemovedFalse()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new ListWorkspacesCliCommand(mediator);
        await cmd.InvokeAsync([]);

        await mediator.Received(1).Send(
            Arg.Is<ListWorkspacesQuery>(q => q.IncludeRemoved == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_IncludeRemovedFlag_SendsQueryWithIncludeRemovedTrue()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new ListWorkspacesCliCommand(mediator);
        var exitCode = await cmd.InvokeAsync(["--include-removed"]);

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<ListWorkspacesQuery>(q => q.IncludeRemoved == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_RemovedWorkspace_TableOutputShowsRemovedSuffix()
    {
        var mediator = Substitute.For<IMediator>();
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "OldRepo",
            Path = @"C:\old",
            IsRemoved = true,
            RemovedAt = DateTimeOffset.UtcNow
        };
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[workspace]);

        var cmd = new ListWorkspacesCliCommand(mediator);
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["--include-removed"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().Contain("OldRepo [removed]");
    }
}

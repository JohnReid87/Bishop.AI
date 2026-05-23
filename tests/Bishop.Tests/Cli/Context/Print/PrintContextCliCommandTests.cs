using Bishop.App.Context;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Context.Print;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.CommandLine;

namespace Bishop.Tests.Cli.Context.Print;

public sealed class PrintContextCliCommandTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly Workspace _workspace = new()
    {
        Id = Guid.NewGuid(),
        Name = "test-ws",
        Path = Directory.GetCurrentDirectory()
    };

    public PrintContextCliCommandTests()
    {
        _mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[_workspace]);
    }

    [Fact]
    public async Task InvokeAsync_NoSection_SendsQueryWithNullSectionName()
    {
        _mediator.Send(Arg.Any<PrintContextQuery>(), Arg.Any<CancellationToken>())
            .Returns("# Sections: This workspace\nfull content");

        var cmd = new PrintContextCliCommand(_mediator);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        await _mediator.Received(1).Send(
            Arg.Is<PrintContextQuery>(q => q.WorkspacePath == _workspace.Path && q.SectionName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithSection_SendsQueryWithSectionName()
    {
        _mediator.Send(Arg.Any<PrintContextQuery>(), Arg.Any<CancellationToken>())
            .Returns("## Shell selection (STABLE)\n\ncontent");

        var cmd = new PrintContextCliCommand(_mediator);
        var exitCode = await cmd.InvokeAsync(["--section", "Shell selection"]);

        exitCode.Should().Be(0);
        await _mediator.Received(1).Send(
            Arg.Is<PrintContextQuery>(q => q.SectionName == "Shell selection"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_HandlerThrows_StillCompletesWithoutUnhandledException()
    {
        _mediator.Send(Arg.Any<PrintContextQuery>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Unknown section"));

        var cmd = new PrintContextCliCommand(_mediator);
        Func<Task> act = async () => { await cmd.InvokeAsync(["--section", "Bogus"]); };

        await act.Should().NotThrowAsync();
    }
}

using Bishop.App.Context.ContextPack;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Context.Pack;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.CommandLine;

namespace Bishop.Tests.Cli.Context.Pack;

[Collection("ConsoleTests")]
public sealed class PrintContextPackCliCommandTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Workspace _workspace = new()
    {
        Id = Guid.NewGuid(),
        Name = "test-ws",
        Path = Directory.GetCurrentDirectory()
    };

    private readonly ContextPack _contextPack = new(
        new WorkspaceBlock("test-ws", Directory.GetCurrentDirectory(), null, ["To Do"], ["test"], null, false),
        new GitBlock("main", []),
        null,
        new Dictionary<string, string>());

    public PrintContextPackCliCommandTests()
    {
        _sender.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[_workspace]);
    }

    [Fact]
    public async Task InvokeAsync_ListFlag_OutputsProvidersAsJsonAndExitsZero()
    {
        var providerA = Substitute.For<IContextProvider>();
        providerA.SkillName.Returns("work-on-card");
        providerA.RequiredSections.Returns(["Shell selection"]);

        var providerB = Substitute.For<IContextProvider>();
        providerB.SkillName.Returns("auto-card");
        providerB.RequiredSections.Returns([]);

        var cmd = new PrintContextPackCliCommand(_sender, [providerA, providerB]);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--list"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        var outputStr = output.ToString();
        outputStr.Should().Contain("auto-card").And.Contain("work-on-card");
        outputStr.IndexOf("auto-card", StringComparison.Ordinal)
            .Should().BeLessThan(outputStr.IndexOf("work-on-card", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_ListFlagWithNoProviders_OutputsEmptyProvidersArrayAndExitsZero()
    {
        var cmd = new PrintContextPackCliCommand(_sender, []);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--list"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("providers");
    }

    [Fact]
    public async Task InvokeAsync_NoSkillName_ExitsOne()
    {
        var cmd = new PrintContextPackCliCommand(_sender, []);

        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_ValidSkillName_SendsQueryAndOutputsJsonAndExitsZero()
    {
        _sender.Send(Arg.Any<BuildContextPackQuery>(), Arg.Any<CancellationToken>())
            .Returns(_contextPack);

        var cmd = new PrintContextPackCliCommand(_sender, []);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["work-on-card"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        await _sender.Received(1).Send(
            Arg.Is<BuildContextPackQuery>(q => q.SkillName == "work-on-card"),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("test-ws");
    }

    [Fact]
    public async Task InvokeAsync_QueryThrowsInvalidOperationException_ExitsOne()
    {
        _sender.Send(Arg.Any<BuildContextPackQuery>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Unknown skill 'bogus'. Known providers: work-on-card"));

        var cmd = new PrintContextPackCliCommand(_sender, []);

        var exitCode = await cmd.InvokeAsync(["bogus"]);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_WithCardOption_PassesCardNumberInQuery()
    {
        _sender.Send(Arg.Any<BuildContextPackQuery>(), Arg.Any<CancellationToken>())
            .Returns(_contextPack);

        var cmd = new PrintContextPackCliCommand(_sender, []);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await cmd.InvokeAsync(["work-on-card", "--card", "42"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        await _sender.Received(1).Send(
            Arg.Is<BuildContextPackQuery>(q => q.Args.Card == 42),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WorkspaceNotFound_ExitsOne()
    {
        _sender.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[]);

        var cmd = new PrintContextPackCliCommand(_sender, []);

        var exitCode = await cmd.InvokeAsync(["work-on-card"]);

        exitCode.Should().Be(1);
    }
}

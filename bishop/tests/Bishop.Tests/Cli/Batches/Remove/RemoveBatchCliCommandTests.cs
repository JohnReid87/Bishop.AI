using Bishop.App.Batches.RemoveBatch;
using Bishop.Cli.Batches.Remove;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Batches.Remove;

[Collection("ConsoleTests")]
public sealed class RemoveBatchCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndSendsRemoveCommand()
    {
        var mediator = Substitute.For<IMediator>();
        var cmd = new RemoveBatchCliCommand(mediator);

        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["Sprint 1"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RemoveBatchCommand>(c => c.Name == "Sprint 1"),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("Batch 'Sprint 1' removed.");
    }
}

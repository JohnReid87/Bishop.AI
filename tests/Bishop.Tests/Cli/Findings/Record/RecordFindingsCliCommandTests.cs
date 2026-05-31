using Bishop.App.Findings.RecordFindings;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Findings.Record;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;

namespace Bishop.Tests.Cli.Findings.Record;

[Collection("ConsoleTests")]
public sealed class RecordFindingsCliCommandTests
{
    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };

    private static (IMediator mediator, RecordFindingsCliCommand cmd) Build(
        Workspace ws, RecordFindingsResult result)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<RecordFindingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return (mediator, new RecordFindingsCliCommand(mediator));
    }

    [Fact]
    public async Task InvokeAsync_WithFileOption_ExitsZeroAndSendsRecordFindingsCommand()
    {
        var ws = DefaultWorkspace();
        var result = new RecordFindingsResult(2);
        var (mediator, cmd) = Build(ws, result);
        var json = """[{"x":1}]""";
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, json);

            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            int exitCode;
            try { exitCode = await cmd.InvokeAsync(["--skill", "bish-arch", "--file", tempFile, "--sha", "abc1234", "--workspace", "test-ws"]); }
            finally { Console.SetOut(originalOut); }

            exitCode.Should().Be(0);
            await mediator.Received(1).Send(
                Arg.Is<RecordFindingsCommand>(c =>
                    c.WorkspaceId == ws.Id &&
                    c.WorkspacePath == ws.Path &&
                    c.SkillName == "bish-arch" &&
                    c.FindingsJson == json &&
                    c.GitSha == "abc1234"),
                Arg.Any<CancellationToken>());
            output.ToString().Should().Contain("Recorded 2 findings for 'bish-arch'");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithProjectOption_InjectsProjectNameIntoPayload()
    {
        var ws = DefaultWorkspace();
        var result = new RecordFindingsResult(0);
        var (mediator, cmd) = Build(ws, result);
        var json = """{"findings":[]}""";
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, json);

            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            int exitCode;
            try
            {
                exitCode = await cmd.InvokeAsync(
                    ["--skill", "bish-tests", "--file", tempFile, "--sha", "abc1234", "--project", "Bishop.App", "--workspace", "test-ws"]);
            }
            finally { Console.SetOut(originalOut); }

            exitCode.Should().Be(0);
            await mediator.Received(1).Send(
                Arg.Is<RecordFindingsCommand>(c =>
                    c.SkillName == "bish-tests" &&
                    c.FindingsJson.Contains("\"projectName\":\"Bishop.App\"")),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithProjectOption_OverridesPayloadProjectName()
    {
        var ws = DefaultWorkspace();
        var result = new RecordFindingsResult(0);
        var (mediator, cmd) = Build(ws, result);
        var json = """{"projectName":"Bishop.UI","findings":[]}""";
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, json);

            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);
            try
            {
                await cmd.InvokeAsync(
                    ["--skill", "bish-tests", "--file", tempFile, "--sha", "abc1234", "--project", "Bishop.App", "--workspace", "test-ws"]);
            }
            finally { Console.SetOut(originalOut); }

            await mediator.Received(1).Send(
                Arg.Is<RecordFindingsCommand>(c =>
                    c.FindingsJson.Contains("\"projectName\":\"Bishop.App\"") &&
                    !c.FindingsJson.Contains("Bishop.UI")),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithStdinOption_ExitsZeroAndSendsRecordFindingsCommand()
    {
        var ws = DefaultWorkspace();
        var result = new RecordFindingsResult(1);
        var (mediator, cmd) = Build(ws, result);
        var json = """[{"x":1}]""";

        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader(json));
        var output = new StringWriter();
        Console.SetOut(output);
        int exitCode;
        try
        {
            exitCode = await cmd.InvokeAsync(["--skill", "bish-security", "--file", "-", "--sha", "def5678", "--workspace", "test-ws"]);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }

        exitCode.Should().Be(0);
        await mediator.Received(1).Send(
            Arg.Is<RecordFindingsCommand>(c =>
                c.WorkspaceId == ws.Id &&
                c.WorkspacePath == ws.Path &&
                c.SkillName == "bish-security" &&
                c.FindingsJson == json &&
                c.GitSha == "def5678"),
            Arg.Any<CancellationToken>());
        output.ToString().Should().Contain("Recorded 1 finding for 'bish-security'");
        output.ToString().Should().NotContain("1 findings");
    }
}

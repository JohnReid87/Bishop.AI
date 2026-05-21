using System.ComponentModel;
using System.Diagnostics;
using Bishop.App.Claude;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Claude;

public sealed class ClaudeCliRunnerTests
{
    [Fact]
    public async Task RunPromptAsync_Throws_WithDiagnosticMessage_WhenResolverReportsMissing()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver
            .When(r => r.Resolve())
            .Do(_ => throw new ClaudeNotFoundException(
                candidates: new[] { "claude.EXE", "claude.CMD" },
                directories: new[] { "C:\\a", "C:\\b" }));
        var sut = new ClaudeCliRunner(resolver);

        var act = async () => await sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.InnerException.Should().BeOfType<ClaudeNotFoundException>();
        ex.Message.Should().Contain("Could not find 'claude' on PATH.");
        ex.Message.Should().Contain("claude.EXE");
        ex.Message.Should().Contain("claude.CMD");
        ex.Message.Should().Contain("C:\\a");
        ex.Message.Should().Contain("C:\\b");
        ex.Message.Should().Contain("https://docs.claude.com");
    }

    [Fact]
    public async Task RunPromptAsync_ReturnsExitCode_WhenProcessExitsNormally()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 42"));

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.Should().Be(42);
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessStartThrowsWin32Exception()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => throw new Win32Exception());

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.InnerException.Should().BeOfType<Win32Exception>();
        ex.Message.Should().Contain("Could not start 'claude'");
        ex.Message.Should().Contain("https://docs.claude.com");
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessStartThrowsFileNotFoundException()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => throw new FileNotFoundException());

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.InnerException.Should().BeOfType<FileNotFoundException>();
        ex.Message.Should().Contain("Could not start 'claude'");
        ex.Message.Should().Contain("https://docs.claude.com");
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessIsNull()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => null);

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Be("Failed to start 'claude' process.");
    }

    [Fact]
    public async Task RunPromptAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 0"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.RunPromptAsync("C:\\ws", "hello", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunPromptAsync_WritesFormattedJsonToConsoleOut()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        Func<ProcessStartInfo, Process?> starter = _ =>
        {
            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "/c more",
            };
            var proc = Process.Start(psi)!;
            proc.StandardInput.WriteLine("{\"type\":\"result\"}");
            proc.StandardInput.Close();
            return proc;
        };
        var sut = new ClaudeCliRunner(resolver, starter);

        var captured = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(captured);
        try
        {
            await sut.RunPromptAsync("C:\\ws", "hello");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        captured.ToString().Should().Contain("done, 0 tool uses");
    }

    [Fact]
    public async Task RunPromptAsync_ForwardsStderrToConsoleError()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        Func<ProcessStartInfo, Process?> starter = _ =>
        {
            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("echo stderr_text 1>&2");
            return Process.Start(psi);
        };
        var sut = new ClaudeCliRunner(resolver, starter);

        var captured = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(captured);
        try
        {
            await sut.RunPromptAsync("C:\\ws", "hello");
        }
        finally
        {
            Console.SetError(originalError);
        }

        captured.ToString().Should().Contain("stderr_text");
    }

    private static Process CmdProcess(string arguments) =>
        Process.Start(new ProcessStartInfo("cmd.exe")
        {
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
}

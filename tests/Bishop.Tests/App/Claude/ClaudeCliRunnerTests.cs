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
    public async Task RunPromptAsync_Throws_WithEmptyPathMessage_WhenResolverReportsMissingAndDirectoriesEmpty()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver
            .When(r => r.Resolve())
            .Do(_ => throw new ClaudeNotFoundException(
                candidates: new[] { "claude.EXE" },
                directories: Array.Empty<string>()));
        var sut = new ClaudeCliRunner(resolver);

        var act = async () => await sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("(PATH was empty)");
    }

    [Fact]
    public async Task RunPromptAsync_ReturnsExitCode_WhenProcessExitsNormally()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 42"));

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(42);
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

        var act = () => sut.RunPromptAsync("C:\\ws", "hello", null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunPromptAsync_ParsesStdoutStreamJson_AndSurfacesTotals()
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
            proc.StandardInput.WriteLine("{\"type\":\"assistant\",\"message\":{\"usage\":{\"input_tokens\":1200,\"output_tokens\":340},\"content\":[{\"type\":\"tool_use\",\"name\":\"Edit\",\"input\":{}}]}}");
            proc.StandardInput.WriteLine("{\"type\":\"result\",\"total_cost_usd\":0.05}");
            proc.StandardInput.Close();
            return proc;
        };
        var sut = new ClaudeCliRunner(resolver, starter);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
        result.ToolUseCount.Should().Be(1);
        result.Totals.Should().Be(new ClaudeRunTotals(0.05m, 1200, 340));
    }

    [Fact]
    public async Task RunPromptAsync_ConsumesStderr_WithoutFailing()
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

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithModel_AppendsModelArgToArgv()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        ProcessStartInfo? capturedPsi = null;
        Func<ProcessStartInfo, Process?> starter = psi =>
        {
            capturedPsi = psi;
            return CmdProcess("/c exit 0");
        };
        var sut = new ClaudeCliRunner(resolver, starter);

        await sut.RunPromptAsync("C:\\ws", "hello", "claude-sonnet-4-6");

        capturedPsi.Should().NotBeNull();
        capturedPsi!.ArgumentList.Should().ContainInOrder("--model", "claude-sonnet-4-6");
    }

    [Fact]
    public async Task RunPromptAsync_WithoutModel_DoesNotAppendModelArg()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        ProcessStartInfo? capturedPsi = null;
        Func<ProcessStartInfo, Process?> starter = psi =>
        {
            capturedPsi = psi;
            return CmdProcess("/c exit 0");
        };
        var sut = new ClaudeCliRunner(resolver, starter);

        await sut.RunPromptAsync("C:\\ws", "hello");

        capturedPsi.Should().NotBeNull();
        capturedPsi!.ArgumentList.Should().NotContain("--model");
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

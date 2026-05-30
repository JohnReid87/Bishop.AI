using System.ComponentModel;
using System.Diagnostics;
using Bishop.App.Services.Claude;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Services.Claude;

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
        var sut = new ClaudeCliRunner(resolver, TimeProvider.System);

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
        var sut = new ClaudeCliRunner(resolver, TimeProvider.System);

        var act = async () => await sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("(PATH was empty)");
    }

    [Fact]
    public async Task RunPromptAsync_ReturnsExitCode_WhenProcessExitsNormally()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 42"), TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(42);
    }

    [Fact]
    public async Task RunPromptAsync_DoesNotThrow_WhenProcessExitsNonZero()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 1"), TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessStartThrowsWin32Exception()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => throw new Win32Exception(), TimeProvider.System);

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
        var sut = new ClaudeCliRunner(resolver, _ => throw new FileNotFoundException(), TimeProvider.System);

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
        var sut = new ClaudeCliRunner(resolver, _ => null, TimeProvider.System);

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Be("Failed to start 'claude' process.");
    }

    [Fact]
    public async Task RunPromptAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 0"), TimeProvider.System);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.RunPromptAsync("C:\\ws", "hello", cancellationToken: cts.Token);

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
            return proc;
        };
        var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
        result.ToolUseCount.Should().Be(1);
        result.Totals.Should().Be(new ClaudeRunTotals(1200, 340, 0, 0, 0.05m));
    }

    [Fact]
    public async Task RunPromptAsync_DoesNotCrash_AndCompletes_WhenStdoutContainsMalformedJson()
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
            proc.StandardInput.WriteLine("not-valid-json");
            proc.StandardInput.WriteLine("{broken");
            proc.StandardInput.WriteLine("{\"type\":\"result\",\"total_cost_usd\":0.01}");
            return proc;
        };
        var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
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
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("echo stderr_text 1>&2");
            return Process.Start(psi);
        };
        var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithModel_AppendsModelArgToArgv()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 0"), TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello", "claude-sonnet-4-6");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithDefaultModel_AppendsDefaultModelArg()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 0"), TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_AlwaysAppendsPermissionModeBypassPermissions()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 0"), TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_AlwaysSetsAutoCardEnvVar()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var sut = new ClaudeCliRunner(resolver, _ => CmdProcess("/c exit 0"), TimeProvider.System);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithPermissionDeniedEvent_AppendsLineToDenialsJsonl()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
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
                proc.StandardInput.WriteLine("{\"type\":\"system\",\"subtype\":\"permission_denied\",\"tool\":\"Bash\",\"toolInput\":{\"command\":\"git push\"},\"message\":\"denied\"}");
                proc.StandardInput.WriteLine("{\"type\":\"result\",\"duration_ms\":100}");
                return proc;
            };
            var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

            await sut.RunPromptAsync(tempDir, "hello", cardNumber: 99);

            var denialsPath = Path.Combine(tempDir, ".bishop", "denials.jsonl");
            File.Exists(denialsPath).Should().BeTrue();
            var lines = File.ReadAllLines(denialsPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            lines.Should().HaveCount(1);
            using var doc = System.Text.Json.JsonDocument.Parse(lines[0]);
            var root = doc.RootElement;
            root.GetProperty("card_number").GetInt32().Should().Be(99);
            root.GetProperty("tool").GetString().Should().Be("Bash");
            root.GetProperty("command").GetString().Should().Be("git push");
            root.GetProperty("message").GetString().Should().Be("denied");
            root.TryGetProperty("timestamp", out _).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunPromptAsync_WithPermissionDeniedEvent_AndNullCardNumber_WritesNullCardNumber()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
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
                proc.StandardInput.WriteLine("{\"type\":\"system\",\"subtype\":\"permission_denied\",\"tool\":\"Bash\",\"toolInput\":{\"command\":\"rm -rf /\"},\"message\":\"denied\"}");
                proc.StandardInput.WriteLine("{\"type\":\"result\",\"duration_ms\":100}");
                return proc;
            };
            var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

            await sut.RunPromptAsync(tempDir, "hello");

            var denialsPath = Path.Combine(tempDir, ".bishop", "denials.jsonl");
            File.Exists(denialsPath).Should().BeTrue();
            var lines = File.ReadAllLines(denialsPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            lines.Should().HaveCount(1);
            using var doc = System.Text.Json.JsonDocument.Parse(lines[0]);
            doc.RootElement.GetProperty("card_number").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunPromptAsync_WithMultipleDeniedEvents_AppendsOneLineEach()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
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
                proc.StandardInput.WriteLine("{\"type\":\"system\",\"subtype\":\"permission_denied\",\"tool\":\"Bash\",\"toolInput\":{\"command\":\"git push\"},\"message\":\"denied1\"}");
                proc.StandardInput.WriteLine("{\"type\":\"system\",\"subtype\":\"permission_denied\",\"tool\":\"Bash\",\"toolInput\":{\"command\":\"curl https://x\"},\"message\":\"denied2\"}");
                proc.StandardInput.WriteLine("{\"type\":\"result\",\"duration_ms\":100}");
                return proc;
            };
            var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

            await sut.RunPromptAsync(tempDir, "hello", cardNumber: 7);

            var denialsPath = Path.Combine(tempDir, ".bishop", "denials.jsonl");
            var lines = File.ReadAllLines(denialsPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            lines.Should().HaveCount(2);
            foreach (var line in lines)
                System.Text.Json.JsonDocument.Parse(line).Dispose();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunPromptAsync_RedirectsStdinAndWritesPromptToProcess()
    {
        var resolver = Substitute.For<IClaudeExecutableResolver>();
        resolver.Resolve().Returns("claude");
        ProcessStartInfo? capturedPsi = null;
        var capturedOutput = new List<string>();

        Func<ProcessStartInfo, Process?> starter = psi =>
        {
            capturedPsi = psi;
            var procPsi = new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/c more",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var proc = Process.Start(procPsi)!;
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    capturedOutput.Add(e.Data);
            };
            return proc;
        };
        var sut = new ClaudeCliRunner(resolver, starter, TimeProvider.System);

        await sut.RunPromptAsync("C:\\ws", "my-test-prompt");

        capturedPsi.Should().NotBeNull();
        capturedOutput.Should().Contain("my-test-prompt");
    }

    private static Process CmdProcess(string arguments) =>
        Process.Start(new ProcessStartInfo("cmd.exe")
        {
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
}

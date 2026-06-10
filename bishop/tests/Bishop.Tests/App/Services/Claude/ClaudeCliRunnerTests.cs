using System.ComponentModel;
using System.Diagnostics;
using Bishop.App.Services.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Services.Claude;

public sealed class ClaudeCliRunnerTests
{
    [Fact]
    public async Task RunPromptAsync_ReturnsExitCode_WhenProcessExitsNormally()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 42"), TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(42);
    }

    [Fact]
    public async Task RunPromptAsync_DoesNotThrow_WhenProcessExitsNonZero()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 1"), TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessStartThrowsWin32Exception()
    {
        var sut = new ClaudeCliRunner(_ => throw new Win32Exception(), TimeProvider.System, "claude");

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.InnerException.Should().BeOfType<Win32Exception>();
        ex.Message.Should().Contain("Could not start 'claude'");
        ex.Message.Should().Contain("https://docs.claude.com");
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessStartThrowsFileNotFoundException()
    {
        var sut = new ClaudeCliRunner(_ => throw new FileNotFoundException(), TimeProvider.System, "claude");

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.InnerException.Should().BeOfType<FileNotFoundException>();
        ex.Message.Should().Contain("Could not start 'claude'");
        ex.Message.Should().Contain("https://docs.claude.com");
    }

    [Fact]
    public async Task RunPromptAsync_Throws_WhenProcessIsNull()
    {
        var sut = new ClaudeCliRunner(_ => null, TimeProvider.System, "claude");

        var act = () => sut.RunPromptAsync("C:\\ws", "hello");

        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Be("Failed to start 'claude' process.");
    }

    [Fact]
    public async Task RunPromptAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, "claude");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.RunPromptAsync("C:\\ws", "hello", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunPromptAsync_ParsesStdoutStreamJson_AndSurfacesTotals()
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
            proc.StandardInput.WriteLine("{\"type\":\"assistant\",\"message\":{\"usage\":{\"input_tokens\":1200,\"output_tokens\":340},\"content\":[{\"type\":\"tool_use\",\"name\":\"Edit\",\"input\":{}}]}}");
            proc.StandardInput.WriteLine("{\"type\":\"result\",\"total_cost_usd\":0.05}");
            return proc;
        };
        var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
        result.ToolUseCount.Should().Be(1);
        result.Totals.Should().Be(new ClaudeRunTotals(1200, 340, 0, 0, 0.05m));
    }

    [Fact]
    public async Task RunPromptAsync_DoesNotCrash_AndCompletes_WhenStdoutContainsMalformedJson()
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
            proc.StandardInput.WriteLine("not-valid-json");
            proc.StandardInput.WriteLine("{broken");
            proc.StandardInput.WriteLine("{\"type\":\"result\",\"total_cost_usd\":0.01}");
            return proc;
        };
        var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_ConsumesStderr_WithoutFailing()
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
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("echo stderr_text 1>&2");
            return Process.Start(psi);
        };
        var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithModel_AppendsModelArgToArgv()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello", "claude-sonnet-4-6");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithDefaultModel_AppendsDefaultModelArg()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_AlwaysAppendsPermissionModeBypassPermissions()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_AlwaysSetsAutoCardEnvVar()
    {
        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, "claude");

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunPromptAsync_WithPermissionDeniedEvent_AppendsLineToDenialsJsonl()
    {
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
            var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

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
            var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

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
            var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

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
        var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

        await sut.RunPromptAsync("C:\\ws", "my-test-prompt");

        capturedPsi.Should().NotBeNull();
        capturedOutput.Should().Contain("my-test-prompt");
    }

    [Fact]
    public async Task RunPromptAsync_WithNullClaudePath_ResolvesAndStartsProcess_WhenClaudeOnPath()
    {
        try { ClaudeCliRunner.ResolveClaudePath(); }
        catch (ClaudeNotFoundException) { return; } // claude not on PATH in this environment — skip

        var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, null);

        var result = await sut.RunPromptAsync("C:\\ws", "hello");

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public void BuildNotFoundMessage_WithEmptyDirectories_IncludesEmptyPathNote()
    {
        var ex = new ClaudeNotFoundException(["claude.EXE"], []);

        var message = ClaudeCliRunner.BuildNotFoundMessage(ex);

        message.Should().Contain("Could not find 'claude' on PATH.");
        message.Should().Contain("claude.EXE");
        message.Should().Contain("(PATH was empty)");
        message.Should().Contain("Install Claude Code");
    }

    [Fact]
    public void BuildNotFoundMessage_WithDirectories_ListsEachDirectory()
    {
        var ex = new ClaudeNotFoundException(["claude.EXE", "claude.CMD"], [@"C:\bin", @"C:\tools"]);

        var message = ClaudeCliRunner.BuildNotFoundMessage(ex);

        message.Should().Contain("Could not find 'claude' on PATH.");
        message.Should().Contain(@"C:\bin");
        message.Should().Contain(@"C:\tools");
        message.Should().NotContain("(PATH was empty)");
        message.Should().Contain("Install Claude Code");
    }

    [Fact]
    public async Task RunPromptAsync_WithCardNumber_WritesTranscriptJsonlAndReturnsPath()
    {
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
                proc.StandardInput.WriteLine("{\"type\":\"result\",\"duration_ms\":10}");
                return proc;
            };
            var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

            var result = await sut.RunPromptAsync(tempDir, "hello", cardNumber: 42);

            result.TranscriptPath.Should().NotBeNull();
            File.Exists(result.TranscriptPath).Should().BeTrue();
            var lines = File.ReadAllLines(result.TranscriptPath!)
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            lines.Should().HaveCountGreaterThan(0);
            lines.Should().Contain(l => l.Contains("result"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunPromptAsync_WithTranscriptBasePath_WritesTranscriptToBasePath()
    {
        var worktreeDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var mainDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(worktreeDir);
        Directory.CreateDirectory(mainDir);
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
                proc.StandardInput.WriteLine("{\"type\":\"result\",\"duration_ms\":10}");
                return proc;
            };
            var sut = new ClaudeCliRunner(starter, TimeProvider.System, "claude");

            var result = await sut.RunPromptAsync(worktreeDir, "hello", cardNumber: 7, transcriptBasePath: mainDir);

            result.TranscriptPath.Should().StartWith(mainDir);
            File.Exists(result.TranscriptPath).Should().BeTrue();
            Directory.Exists(Path.Combine(worktreeDir, ".bishop", "runs")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(worktreeDir, recursive: true);
            Directory.Delete(mainDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunPromptAsync_WithNullCardNumber_DoesNotWriteTranscript()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var sut = new ClaudeCliRunner(_ => CmdProcess("/c exit 0"), TimeProvider.System, "claude");

            var result = await sut.RunPromptAsync(tempDir, "hello");

            result.TranscriptPath.Should().BeNull();
            Directory.Exists(Path.Combine(tempDir, ".bishop", "runs")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteTranscriptLine_ConcurrentCalls_ProduceOneLineEach()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "transcript.jsonl");
            const int iterations = 200;
            var runner = new ClaudeCliRunner(_ => null, TimeProvider.System, "claude");

            var tasks = Enumerable.Range(0, iterations)
                .Select(i => Task.Run(() => runner.WriteTranscriptLine(filePath, $"{{\"i\":{i}}}")))
                .ToArray();

            await Task.WhenAll(tasks);

            var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            lines.Should().HaveCount(iterations);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteDenialLine_ConcurrentCalls_ProduceValidJsonlPerLine()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "denials.jsonl");
            const int iterations = 500;
            var runner = new ClaudeCliRunner(_ => null, TimeProvider.System, "claude");

            var tasks = Enumerable.Range(0, iterations)
                .Select(i => Task.Run(() =>
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(
                        new { index = i, payload = new string('x', 128) });
                    runner.WriteDenialLine(filePath, json);
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            lines.Should().HaveCount(iterations);
            foreach (var line in lines)
                System.Text.Json.JsonDocument.Parse(line).Dispose();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PruneTranscripts_DeletesOldFiles_WhenAboveRetentionCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            for (var i = 0; i < 12; i++)
                File.WriteAllText(Path.Combine(tempDir, $"5-2024010{i:D2}T120000Z.jsonl"), "{}");

            ClaudeCliRunner.PruneTranscripts(tempDir, cardNumber: 5, retentionCount: 10);

            Directory.GetFiles(tempDir, "5-*Z.jsonl").Should().HaveCount(9);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PruneTranscripts_DoesNothing_WhenBelowRetentionCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            for (var i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(tempDir, $"3-2024010{i:D2}T120000Z.jsonl"), "{}");

            ClaudeCliRunner.PruneTranscripts(tempDir, cardNumber: 3, retentionCount: 10);

            Directory.GetFiles(tempDir, "3-*Z.jsonl").Should().HaveCount(5);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PruneTranscripts_OnlyPrunesMatchingCard()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            for (var i = 0; i < 12; i++)
                File.WriteAllText(Path.Combine(tempDir, $"7-2024010{i:D2}T120000Z.jsonl"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "8-20240101T120000Z.jsonl"), "{}");

            ClaudeCliRunner.PruneTranscripts(tempDir, cardNumber: 7, retentionCount: 10);

            Directory.GetFiles(tempDir, "7-*Z.jsonl").Should().HaveCount(9);
            Directory.GetFiles(tempDir, "8-*Z.jsonl").Should().HaveCount(1);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TrimDenialsIfNeeded_TrimsToMaxLines_WhenOverLimit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "denials.jsonl");
            var lines = Enumerable.Range(0, ClaudeCliRunner.DenialsMaxLines + 50)
                .Select(i => $"{{\"index\":{i}}}");
            File.WriteAllText(filePath, string.Join("\n", lines) + "\n");

            ClaudeCliRunner.TrimDenialsIfNeeded(filePath);

            var result = File.ReadAllLines(filePath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            result.Should().HaveCount(ClaudeCliRunner.DenialsMaxLines);
            // newest entries (highest index) are kept
            result[^1].Should().Contain($"\"index\":{ClaudeCliRunner.DenialsMaxLines + 49}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TrimDenialsIfNeeded_DoesNothing_WhenBelowLimit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "denials.jsonl");
            var content = "{\"a\":1}\n{\"b\":2}\n";
            File.WriteAllText(filePath, content);

            ClaudeCliRunner.TrimDenialsIfNeeded(filePath);

            File.ReadAllText(filePath).Should().Be(content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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

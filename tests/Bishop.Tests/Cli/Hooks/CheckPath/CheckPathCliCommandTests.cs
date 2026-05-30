using Bishop.Cli.Hooks.CheckPath;
using FluentAssertions;
using System.CommandLine;
using System.Text.Json;

namespace Bishop.Tests.Cli.Hooks.CheckPath;

public sealed class CheckPathCliCommandTests : IDisposable
{
    private readonly string _workspaceDir;
    private readonly TextReader _previousIn;
    private readonly string? _previousEnv;

    public CheckPathCliCommandTests()
    {
        _workspaceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_workspaceDir);
        _previousIn = Console.In;
        _previousEnv = Environment.GetEnvironmentVariable("BISHOP_AUTO_CARD");
        Environment.ExitCode = 0;
    }

    public void Dispose()
    {
        Console.SetIn(_previousIn);
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", _previousEnv);
        Environment.ExitCode = 0;
        if (Directory.Exists(_workspaceDir))
            Directory.Delete(_workspaceDir, recursive: true);
    }

    private static string EditPayload(string filePath) =>
        JsonSerializer.Serialize(new
        {
            hook_event_name = "PreToolUse",
            tool_name = "Edit",
            tool_input = new { file_path = filePath }
        });

    private static string WritePayload(string filePath) =>
        JsonSerializer.Serialize(new
        {
            hook_event_name = "PreToolUse",
            tool_name = "Write",
            tool_input = new { file_path = filePath }
        });

    private static string NotebookPayload(string notebookPath) =>
        JsonSerializer.Serialize(new
        {
            hook_event_name = "PreToolUse",
            tool_name = "NotebookEdit",
            tool_input = new { notebook_path = notebookPath }
        });

    [Fact]
    public async Task InvokeAsync_AutoCardNotSet_ExitsZeroWithoutBlocking()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", null);
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "file.cs");
        Console.SetIn(new StringReader(EditPayload(outsidePath)));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(_workspaceDir, ".bishop", "denials.jsonl")).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_PathInsideWorkspace_ExitsZero()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var insidePath = Path.Combine(_workspaceDir, "src", "foo.cs");
        Console.SetIn(new StringReader(EditPayload(insidePath)));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(_workspaceDir, ".bishop", "denials.jsonl")).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_EditOutsideWorkspace_ExitsOneAndLogsDenial()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "evil.cs");
        Console.SetIn(new StringReader(EditPayload(outsidePath)));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(1);
        var denialPath = Path.Combine(_workspaceDir, ".bishop", "denials.jsonl");
        File.Exists(denialPath).Should().BeTrue();
        var line = (await File.ReadAllLinesAsync(denialPath)).Should().ContainSingle().Subject;
        line.Should().Contain("\"tool\":\"Edit\"");
        line.Should().Contain("\"message\":\"Path outside workspace\"");
        line.Should().Contain("\"card_number\":null");
    }

    [Fact]
    public async Task InvokeAsync_WriteOutsideWorkspace_ExitsOneAndLogsDenial()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "evil.txt");
        Console.SetIn(new StringReader(WritePayload(outsidePath)));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(1);
        var denialPath = Path.Combine(_workspaceDir, ".bishop", "denials.jsonl");
        var line = (await File.ReadAllLinesAsync(denialPath)).Should().ContainSingle().Subject;
        line.Should().Contain("\"tool\":\"Write\"");
    }

    [Fact]
    public async Task InvokeAsync_NotebookEditOutsideWorkspace_ExitsOneAndLogsDenial()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "evil.ipynb");
        Console.SetIn(new StringReader(NotebookPayload(outsidePath)));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(1);
        var denialPath = Path.Combine(_workspaceDir, ".bishop", "denials.jsonl");
        var line = (await File.ReadAllLinesAsync(denialPath)).Should().ContainSingle().Subject;
        line.Should().Contain("\"tool\":\"NotebookEdit\"");
    }

    [Fact]
    public async Task InvokeAsync_NonFileEditingTool_ExitsZero()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var payload = JsonSerializer.Serialize(new
        {
            hook_event_name = "PreToolUse",
            tool_name = "Bash",
            tool_input = new { command = "rm -rf /" }
        });
        Console.SetIn(new StringReader(payload));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_EmptyStdin_ExitsZero()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        Console.SetIn(new StringReader(string.Empty));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_MalformedJson_ExitsZero()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        Console.SetIn(new StringReader("not-json{{{"));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        var exitCode = await cmd.InvokeAsync([]);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_DefaultConstructor_PathOutsideCwd_ExitsOneAndLogsDenial()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "evil.cs");
        Console.SetIn(new StringReader(EditPayload(outsidePath)));

        var previousCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_workspaceDir);
        try
        {
            var cmd = new CheckPathCliCommand(TimeProvider.System);
            var exitCode = await cmd.InvokeAsync([]);

            exitCode.Should().Be(1);
            var denialPath = Path.Combine(_workspaceDir, ".bishop", "denials.jsonl");
            File.Exists(denialPath).Should().BeTrue();
            var line = (await File.ReadAllLinesAsync(denialPath)).Should().ContainSingle().Subject;
            line.Should().Contain("\"tool\":\"Edit\"");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }

    [Fact]
    public async Task InvokeAsync_DenialLogEntry_HasSnakeCaseKeysAndNullCardNumber()
    {
        Environment.SetEnvironmentVariable("BISHOP_AUTO_CARD", "1");
        var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "evil.cs");
        Console.SetIn(new StringReader(EditPayload(outsidePath)));

        var cmd = new CheckPathCliCommand(TimeProvider.System, _workspaceDir);
        await cmd.InvokeAsync([]);

        var denialPath = Path.Combine(_workspaceDir, ".bishop", "denials.jsonl");
        var line = (await File.ReadAllLinesAsync(denialPath)).Should().ContainSingle().Subject;
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
        root.TryGetProperty("card_number", out var cardNumberEl).Should().BeTrue();
        cardNumberEl.ValueKind.Should().Be(JsonValueKind.Null);
        root.TryGetProperty("tool", out _).Should().BeTrue();
        root.TryGetProperty("command", out _).Should().BeTrue();
        root.TryGetProperty("message", out _).Should().BeTrue();
    }
}

using System.Text.Json.Nodes;
using Bishop.Life.Core;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class ClaudeSessionJsonlTailerTests
{
    [Fact]
    public void ProcessLine_EmitsUserMessage_ForPlainUserPrompt()
    {
        using var tailer = NewTailer(out var captured);
        var line = """{"type":"user","message":{"content":"hello there"}}""";

        tailer.ProcessLine(line);

        captured.Users.Should().ContainSingle().Which.Should().Be("hello there");
        captured.Assistants.Should().BeEmpty();
        captured.Tools.Should().BeEmpty();
    }

    [Fact]
    public void ProcessLine_EmitsUserMessage_ForArrayContent()
    {
        using var tailer = NewTailer(out var captured);
        var line = """{"type":"user","message":{"content":[{"type":"text","text":"hi"}]}}""";

        tailer.ProcessLine(line);

        captured.Users.Should().ContainSingle().Which.Should().Be("hi");
    }

    [Fact]
    public void ProcessLine_SkipsToolResultUserEnvelopes()
    {
        using var tailer = NewTailer(out var captured);
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","content":"42"}]}}""";

        tailer.ProcessLine(line);

        captured.Users.Should().BeEmpty();
    }

    [Fact]
    public void ProcessLine_StripsSystemReminderAndCommandMeta()
    {
        using var tailer = NewTailer(out var captured);
        var line = """{"type":"user","message":{"content":"<system-reminder>\nignore me\n</system-reminder>\nrun the stand-up"}}""";

        tailer.ProcessLine(line);

        captured.Users.Should().ContainSingle().Which.Should().Be("run the stand-up");
    }

    [Fact]
    public void ProcessLine_DropsMetaOnlyUserMessages()
    {
        using var tailer = NewTailer(out var captured);
        var line = """{"type":"user","message":{"content":"<command-name>/bish-life-standup</command-name><command-message>bish-life-standup</command-message>"}}""";

        tailer.ProcessLine(line);

        captured.Users.Should().BeEmpty();
    }

    [Fact]
    public void ProcessLine_EmitsAssistantTextAndToolUse_InOrder()
    {
        using var tailer = NewTailer(out var captured);
        var line = """{"type":"assistant","message":{"content":[{"type":"text","text":"Let me read the file."},{"type":"tool_use","name":"Read","input":{"file_path":"C:/x/y.cs"}}]}}""";

        tailer.ProcessLine(line);

        captured.Assistants.Should().ContainSingle().Which.Should().Be("Let me read the file.");
        captured.Tools.Should().ContainSingle().Which.Summary.Should().Be("reading C:/x/y.cs");
        captured.Tools[0].Name.Should().Be("Read");
    }

    [Fact]
    public void ProcessLine_IgnoresMalformedJson()
    {
        using var tailer = NewTailer(out var captured);
        tailer.ProcessLine("not json at all");
        tailer.ProcessLine("");
        captured.Users.Should().BeEmpty();
        captured.Assistants.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Glob", "pattern", "src/**/*.cs", "globbing src/**/*.cs")]
    [InlineData("Grep", "pattern", "TODO", "grepping TODO")]
    [InlineData("Bash", "command", "git status", "running git status")]
    [InlineData("Write", "file_path", "C:/x/y.cs", "writing C:/x/y.cs")]
    [InlineData("Edit", "file_path", "C:/x/y.cs", "editing C:/x/y.cs")]
    [InlineData("WebFetch", "url", "https://x.test", "fetching https://x.test")]
    [InlineData("UnknownTool", "whatever", "ignored", "UnknownTool")]
    public void SummariseToolUse_MapsCommonTools(string name, string key, string value, string expected)
    {
        var input = new JsonObject { [key] = value };
        ClaudeSessionJsonlTailer.SummariseToolUse(name, input).Should().Be(expected);
    }

    [Fact]
    public void SummariseToolUse_TodoWrite_IsConstant()
    {
        ClaudeSessionJsonlTailer.SummariseToolUse("TodoWrite", new JsonObject()).Should().Be("updating todos");
    }

    [Fact]
    public void SummariseToolUse_TruncatesLongInput()
    {
        var longCmd = new string('x', 200);
        var summary = ClaudeSessionJsonlTailer.SummariseToolUse("Bash", new JsonObject { ["command"] = longCmd });
        summary.Length.Should().BeLessThan(longCmd.Length);
        summary.Should().EndWith("…");
    }

    private static ClaudeSessionJsonlTailer NewTailer(out CapturedEvents captured)
    {
        var dir = Path.Combine(Path.GetTempPath(), "bishop-life-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var tailer = new ClaudeSessionJsonlTailer(Path.Combine(dir, "session.jsonl"));
        captured = new CapturedEvents();
        WireCapture(tailer, captured);
        return tailer;
    }

    private static void WireCapture(ClaudeSessionJsonlTailer tailer, CapturedEvents captured)
    {
        tailer.UserMessage += t => captured.Users.Add(t);
        tailer.AssistantText += t => captured.Assistants.Add(t);
        tailer.ToolUse += t => captured.Tools.Add(t);
    }

    private sealed class CapturedEvents
    {
        public List<string> Users { get; } = new();
        public List<string> Assistants { get; } = new();
        public List<ClaudeSessionJsonlTailer.ToolUseEvent> Tools { get; } = new();
    }
}

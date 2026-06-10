using Bishop.Life.Core;
using FluentAssertions;

namespace Bishop.Life.Tests;

/// <summary>
/// Snapshot tests for <see cref="ClaudeSessionJsonlTailer"/> driven by real
/// Claude Code session JSONL fixtures under <c>Fixtures/jsonl/</c>. Tail the
/// lines through <see cref="ClaudeSessionJsonlTailer.ProcessLine"/> and assert
/// the transcript event(s) the production code raises. When Claude Code
/// changes the JSONL shape, replace the fixture with a fresh capture; if the
/// new shape no longer parses, these tests fail loudly instead of silently
/// dropping the event (card #1074).
/// </summary>
public class ClaudeSessionJsonlTailerFixtureTests
{
    [Fact]
    public void UserFixture_RaisesUserMessage_WithPlainContent()
    {
        var captured = ReplayFixture("user.jsonl");

        captured.Users.Should().ContainSingle()
            .Which.Should().Be("y and push the follow ip singleton ticket");
        captured.Assistants.Should().BeEmpty();
        captured.Tools.Should().BeEmpty();
        captured.ParseFailures.Should().BeEmpty();
    }

    [Fact]
    public void AssistantFixture_RaisesAssistantText()
    {
        var captured = ReplayFixture("assistant.jsonl");

        captured.Assistants.Should().ContainSingle()
            .Which.Should().Be("**Workspace:** bishop.ai");
        captured.Users.Should().BeEmpty();
        captured.Tools.Should().BeEmpty();
        captured.ParseFailures.Should().BeEmpty();
    }

    [Fact]
    public void ToolFixture_RaisesToolUse_WithSummarisedCommand()
    {
        var captured = ReplayFixture("tool.jsonl");

        captured.Tools.Should().ContainSingle();
        captured.Tools[0].Name.Should().Be("Bash");
        captured.Tools[0].Summary.Should().Be("running bishop workspace current --json");
        captured.Users.Should().BeEmpty();
        captured.Assistants.Should().BeEmpty();
        captured.ParseFailures.Should().BeEmpty();
    }

    [Fact]
    public void SystemFixture_IsRecognisedAsKnownNoOp()
    {
        // The "system" type carries turn-duration telemetry and produces no
        // transcript event — but it must not raise ParseFailed either, or
        // every stand-up would post a spurious system note.
        var captured = ReplayFixture("system.jsonl");

        captured.Users.Should().BeEmpty();
        captured.Assistants.Should().BeEmpty();
        captured.Tools.Should().BeEmpty();
        captured.ParseFailures.Should().BeEmpty();
    }

    private static CapturedEvents ReplayFixture(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jsonl", fixtureName);
        File.Exists(path).Should().BeTrue($"fixture '{fixtureName}' must be copied to the test output directory");

        var dir = Path.Combine(Path.GetTempPath(), "bishop-life-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var tailer = new ClaudeSessionJsonlTailer(Path.Combine(dir, "session.jsonl"));
        var captured = new CapturedEvents();
        tailer.UserMessage += t => captured.Users.Add(t);
        tailer.AssistantText += t => captured.Assistants.Add(t);
        tailer.ToolUse += t => captured.Tools.Add(t);
        tailer.ParseFailed += t => captured.ParseFailures.Add(t);

        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            tailer.ProcessLine(line, i + 1);
        }
        return captured;
    }

    private sealed class CapturedEvents
    {
        public List<string> Users { get; } = new();
        public List<string> Assistants { get; } = new();
        public List<ClaudeSessionJsonlTailer.ToolUseEvent> Tools { get; } = new();
        public List<ClaudeSessionJsonlTailer.ParseFailedEvent> ParseFailures { get; } = new();
    }
}

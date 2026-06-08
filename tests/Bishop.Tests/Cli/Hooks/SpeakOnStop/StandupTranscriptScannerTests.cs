using Bishop.Cli.Hooks.SpeakOnStop;
using FluentAssertions;
using System.Text.Json;

namespace Bishop.Tests.Cli.Hooks.SpeakOnStop;

public sealed class StandupTranscriptScannerTests : IDisposable
{
    private readonly string _transcriptPath;

    public StandupTranscriptScannerTests()
    {
        _transcriptPath = Path.Combine(Path.GetTempPath(), $"bishop-tx-{Guid.NewGuid()}.jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_transcriptPath))
            File.Delete(_transcriptPath);
    }

    private void WriteTranscript(params string[] lines) => File.WriteAllLines(_transcriptPath, lines);

    private static string TextLine(string role, string text)
    {
        var encoded = JsonSerializer.Serialize(text);
        return "{\"type\":\"" + role + "\",\"message\":{\"role\":\"" + role + "\",\"content\":[{\"type\":\"text\",\"text\":" + encoded + "}]}}";
    }

    private static string User(string text) => TextLine("user", text);
    private static string Assistant(string text) => TextLine("assistant", text);

    [Fact]
    public void Returns_text_when_last_command_is_standup_skill()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("What's on top of mind today?"));

        var result = StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text);

        result.Should().BeTrue();
        text.Should().Be("What's on top of mind today?");
    }

    [Fact]
    public void Returns_false_when_no_command_marker()
    {
        WriteTranscript(
            User("just a regular message"),
            Assistant("hello"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }

    [Fact]
    public void Returns_false_when_last_command_is_different_skill()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("standup turn"),
            User("<command-name>/bish-work-on-card</command-name>"),
            Assistant("working on card now"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }

    [Fact]
    public void Picks_most_recent_assistant_text_within_standup()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("first turn"),
            User("ok, continue"),
            Assistant("second turn"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("second turn");
    }

    [Fact]
    public void Returns_false_when_transcript_missing()
    {
        StandupTranscriptScanner.TryGetTextToSpeak(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Skips_unparseable_lines()
    {
        WriteTranscript(
            "not json at all",
            User("<command-name>/bish-life-standup</command-name>"),
            "{broken",
            Assistant("survived"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("survived");
    }

    [Fact]
    public void Returns_false_when_standup_active_but_no_assistant_message_yet()
    {
        WriteTranscript(User("<command-name>/bish-life-standup</command-name>"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }
}

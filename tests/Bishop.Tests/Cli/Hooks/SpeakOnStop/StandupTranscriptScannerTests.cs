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

    [Fact]
    public void Strips_single_no_speak_block()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("Intro line.\n<!-- no-speak -->\nNoisy context goes here.\n<!-- /no-speak -->\nWhat's on your mind?"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("Noisy context");
        text.Should().Contain("Intro line.");
        text.Should().Contain("What's on your mind?");
    }

    [Fact]
    public void Strips_multiple_no_speak_blocks()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("Reflection prose.\n<!-- no-speak -->focus list<!-- /no-speak -->\nMore prose.\n<!-- no-speak -->mutations<!-- /no-speak -->\nFinal question?"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("focus list");
        text.Should().NotContain("mutations");
        text.Should().Contain("Reflection prose.");
        text.Should().Contain("More prose.");
        text.Should().Contain("Final question?");
    }

    [Fact]
    public void Leaves_unclosed_no_speak_marker_intact()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("Before.\n<!-- no-speak -->\nContent that never closes."));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Contain("Content that never closes.");
    }

    [Fact]
    public void Strips_markdown_emphasis_backticks_and_bullets()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("**Bold heading**\n- bullet one\n* bullet two\n• bullet three\nUse `code` and _italic_ and *emphasis* here.\n# Heading text"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("**");
        text.Should().NotContain("`");
        text.Should().NotContain("- bullet");
        text.Should().NotContain("* bullet");
        text.Should().NotContain("• bullet");
        text.Should().NotContain("# Heading");
        text.Should().Contain("Bold heading");
        text.Should().Contain("bullet one");
        text.Should().Contain("bullet two");
        text.Should().Contain("bullet three");
        text.Should().Contain("code");
        text.Should().Contain("italic");
        text.Should().Contain("emphasis");
        text.Should().Contain("Heading text");
    }

    [Fact]
    public void Strips_separator_chars()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("Finances ▸ Emergency fund ▸ Move money\nA › B › C"));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("▸");
        text.Should().NotContain("›");
        text.Should().Contain("Finances");
        text.Should().Contain("Emergency fund");
    }

    [Fact]
    public void Strips_no_speak_then_strips_markdown_from_remainder()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("<!-- no-speak -->**Last stand-up:** 2 days ago\n• Item one<!-- /no-speak -->\n**Reflection:** today went *well*."));

        StandupTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("Last stand-up");
        text.Should().NotContain("Item one");
        text.Should().NotContain("**");
        text.Should().NotContain("*well*");
        text.Should().Contain("Reflection:");
        text.Should().Contain("well");
    }
}

using Bishop.Cli.Hooks.SpeakOnStop;
using FluentAssertions;
using System.Text.Json;

namespace Bishop.Tests.Cli.Hooks.SpeakOnStop;

public sealed class BishLifeTranscriptScannerTests : IDisposable
{
    private readonly string _transcriptPath;

    public BishLifeTranscriptScannerTests()
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

        var result = BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text);

        result.Should().BeTrue();
        text.Should().Be("What's on top of mind today?");
    }

    [Fact]
    public void Returns_text_when_last_command_is_life_add_skill()
    {
        WriteTranscript(
            User("<command-name>/bish-life-add</command-name>"),
            Assistant("What just came to mind?"));

        var result = BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text);

        result.Should().BeTrue();
        text.Should().Be("What just came to mind?");
    }

    [Fact]
    public void Returns_false_when_no_command_marker()
    {
        WriteTranscript(
            User("just a regular message"),
            Assistant("hello"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }

    [Fact]
    public void Returns_false_when_last_command_is_different_skill()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("standup turn"),
            User("<command-name>/bish-work-on-card</command-name>"),
            Assistant("working on card now"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }

    [Fact]
    public void Returns_false_when_last_command_is_unlisted_bish_life_skill()
    {
        WriteTranscript(
            User("<command-name>/bish-life-init</command-name>"),
            Assistant("initialised"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }

    [Fact]
    public void Picks_most_recent_assistant_text_within_standup()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("first turn"),
            User("ok, continue"),
            Assistant("second turn"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("second turn");
    }

    [Fact]
    public void Returns_false_when_transcript_missing()
    {
        BishLifeTranscriptScanner.TryGetTextToSpeak(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), out _)
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

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("survived");
    }

    [Fact]
    public void Returns_false_when_standup_active_but_no_assistant_message_yet()
    {
        WriteTranscript(User("<command-name>/bish-life-standup</command-name>"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out _).Should().BeFalse();
    }

    [Fact]
    public void Strips_single_no_speak_block()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("Intro line.\n<!-- no-speak -->\nNoisy context goes here.\n<!-- /no-speak -->\nWhat's on your mind?"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
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

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
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

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Contain("Content that never closes.");
    }

    [Fact]
    public void Strips_markdown_emphasis_backticks_and_bullets()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("**Bold heading**\n- bullet one\n* bullet two\n• bullet three\nUse `code` and _italic_ and *emphasis* here.\n# Heading text"));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
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

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("▸");
        text.Should().NotContain("›");
        text.Should().Contain("Finances");
        text.Should().Contain("Emergency fund");
    }

    [Fact]
    public void Life_add_echo_prompt_speaks_only_the_captured_phrase()
    {
        var assistant =
            "<!-- no-speak -->Capture as:<!-- /no-speak --> \"book the dentist\" " +
            "<!-- no-speak -->— keep? (`y` / paste a correction / `n` to drop)<!-- /no-speak -->";
        WriteTranscript(
            User("<command-name>/bish-life-add</command-name>"),
            Assistant(assistant));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("\"book the dentist\"");
    }

    [Fact]
    public void Life_add_uninitialised_refusal_speaks_only_the_short_message()
    {
        var assistant =
            "bishop.life is not initialised. " +
            "<!-- no-speak -->Path: `C:\\Users\\j\\AppData\\Roaming\\Bishop\\life\\bishop.life.json`. " +
            "Run `/bish-life-init` first.<!-- /no-speak -->";
        WriteTranscript(
            User("<command-name>/bish-life-add</command-name>"),
            Assistant(assistant));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("bishop.life is not initialised.");
    }

    [Fact]
    public void Life_add_capture_confirmation_speaks_only_the_acknowledgement()
    {
        var assistant =
            "Captured 2 inbox items. " +
            "<!-- no-speak -->Backup at `C:\\Users\\j\\AppData\\Roaming\\Bishop\\life\\bishop.life.json.prev` " +
            "— delete or restore by hand if you need to undo.<!-- /no-speak -->";
        WriteTranscript(
            User("<command-name>/bish-life-add</command-name>"),
            Assistant(assistant));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("Captured 2 inbox items.");
    }

    [Fact]
    public void Life_standup_step6_confirm_speaks_reflection_and_question_only()
    {
        var assistant =
            "Today was a sort: emergency fund moved up a notch, the dentist is finally booked, " +
            "and the side-project rewrite is parked until July.\n\n" +
            "<!-- no-speak -->\n" +
            "**Focus today:**\n" +
            "- Finances ▸ Emergency fund ▸ Move £500\n" +
            "- Health ▸ Sleep ▸ Phone out of bedroom\n\n" +
            "**Mutations:**\n" +
            "- new action `act-xyz` under Finances\n" +
            "- starred `act-abc` removed (over ceiling)\n" +
            "<!-- /no-speak -->\n\n" +
            "Write this stand-up? <!-- no-speak -->(`y` to commit / `n` to abandon — nothing has been written yet)<!-- /no-speak -->";
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant(assistant));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Contain("Today was a sort");
        text.Should().Contain("Write this stand-up?");
        text.Should().NotContain("Focus today");
        text.Should().NotContain("Mutations");
        text.Should().NotContain("Move £500");
        text.Should().NotContain("act-xyz");
        text.Should().NotContain("`y`");
        text.Should().NotContain("abandon");
        text.Should().NotContain("▸");
    }

    [Fact]
    public void Life_standup_saved_confirmation_speaks_only_the_acknowledgement()
    {
        var assistant =
            "Stand-up saved. " +
            "<!-- no-speak -->Backup at `C:\\Users\\j\\AppData\\Roaming\\Bishop\\life\\bishop.life.json.prev` " +
            "— delete or restore by hand if you need to undo.<!-- /no-speak -->";
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant(assistant));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().Be("Stand-up saved.");
    }

    [Fact]
    public void Life_standup_context_pack_block_speaks_only_the_brain_dump_prompt()
    {
        var assistant =
            "<!-- no-speak -->\n" +
            "**Last stand-up:** 2 days ago (2026-06-06)\n" +
            "**Open actions:** 7\n" +
            "**Starred (2/3):**\n" +
            "  • Finances ▸ Emergency fund ▸ Move £500\n" +
            "  • Health ▸ Sleep ▸ Phone out\n" +
            "**Untended areas:** Career, Relationships\n" +
            "**Inbox (2):**\n" +
            "  • Look into ISA limits\n" +
            "  • Book dentist\n" +
            "<!-- /no-speak -->\n\n" +
            "What's on your mind? Anything from the last few days — wins, worries, things you've been putting off.";
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant(assistant));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().StartWith("What's on your mind?");
        text.Should().NotContain("Last stand-up");
        text.Should().NotContain("Open actions");
        text.Should().NotContain("Starred");
        text.Should().NotContain("Untended");
        text.Should().NotContain("Inbox");
        text.Should().NotContain("ISA");
    }

    [Fact]
    public void Strips_no_speak_then_strips_markdown_from_remainder()
    {
        WriteTranscript(
            User("<command-name>/bish-life-standup</command-name>"),
            Assistant("<!-- no-speak -->**Last stand-up:** 2 days ago\n• Item one<!-- /no-speak -->\n**Reflection:** today went *well*."));

        BishLifeTranscriptScanner.TryGetTextToSpeak(_transcriptPath, out var text).Should().BeTrue();
        text.Should().NotContain("Last stand-up");
        text.Should().NotContain("Item one");
        text.Should().NotContain("**");
        text.Should().NotContain("*well*");
        text.Should().Contain("Reflection:");
        text.Should().Contain("well");
    }
}

using Bishop.App.Skills;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class ClaudeModelsTests
{
    [Fact]
    public void DisplayFor_WhenOpus48_ReturnsOpus48Display()
    {
        ClaudeModels.DisplayFor(ClaudeModels.Opus48).Should().Be(ClaudeModels.Opus48Display);
    }

    [Fact]
    public void DisplayFor_WhenOpus47_ReturnsOpus47Display()
    {
        ClaudeModels.DisplayFor(ClaudeModels.Opus47).Should().Be(ClaudeModels.Opus47Display);
    }

    [Fact]
    public void DisplayFor_WhenSonnet5_ReturnsSonnet5Display()
    {
        ClaudeModels.DisplayFor(ClaudeModels.Sonnet5).Should().Be(ClaudeModels.Sonnet5Display);
    }

    [Fact]
    public void DisplayFor_WhenSonnet46_ReturnsSonnet46Display()
    {
        ClaudeModels.DisplayFor(ClaudeModels.Sonnet46).Should().Be(ClaudeModels.Sonnet46Display);
    }

    [Fact]
    public void DisplayFor_WhenHaiku45_ReturnsHaiku45Display()
    {
        ClaudeModels.DisplayFor(ClaudeModels.Haiku45).Should().Be(ClaudeModels.Haiku45Display);
    }

    [Fact]
    public void DisplayFor_WhenUnknownModelId_ReturnsRawId()
    {
        const string unknown = "claude-unknown-99-9";

        ClaudeModels.DisplayFor(unknown).Should().Be(unknown);
    }
}

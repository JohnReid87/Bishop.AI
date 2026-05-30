using Bishop.App.Services.Claude;
using FluentAssertions;

namespace Bishop.Tests.App.Claude;

public sealed class ClaudeNotFoundExceptionTests
{
    [Fact]
    public void Message_HasExpectedText()
    {
        var sut = new ClaudeNotFoundException(
            candidates: new[] { "claude.EXE" },
            directories: new[] { "C:\\a" });

        sut.Message.Should().Be("Could not find 'claude' on PATH.");
    }

    [Fact]
    public void Constructor_ExposesCandidatesAndDirectories()
    {
        var candidates = new[] { "claude.EXE", "claude.CMD" };
        var directories = new[] { "C:\\a", "C:\\b" };

        var sut = new ClaudeNotFoundException(candidates, directories);

        sut.Candidates.Should().BeEquivalentTo(candidates);
        sut.Directories.Should().BeEquivalentTo(directories);
    }
}

using Bishop.App.Claude;
using FluentAssertions;
using NSubstitute;

namespace Bishop.Tests.App.Claude;

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
        var sut = new ClaudeCliRunner(resolver);

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
}

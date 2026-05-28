using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class CreateGitProcessStartInfoTests
{
    [Fact]
    public void CreateGitProcessStartInfo_SetsGitTerminalPromptToZero()
    {
        // Arrange / Act
        var psi = GitCli.CreateGitProcessStartInfo("/some/path");

        // Assert
        psi.Environment["GIT_TERMINAL_PROMPT"].Should().Be("0");
    }

    [Fact]
    public void CreateGitProcessStartInfo_SetsWorkingDirectory()
    {
        // Arrange / Act
        var psi = GitCli.CreateGitProcessStartInfo("/test/dir");

        // Assert
        psi.WorkingDirectory.Should().Be("/test/dir");
        psi.FileName.Should().Be("git");
        psi.UseShellExecute.Should().BeFalse();
        psi.CreateNoWindow.Should().BeTrue();
        psi.RedirectStandardOutput.Should().BeTrue();
        psi.RedirectStandardError.Should().BeTrue();
    }
}

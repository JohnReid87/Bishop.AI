using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class PushAsyncTests : IDisposable
{
    private readonly string _tempDir;

    public PushAsyncTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Git creates read-only files in .git/objects/ on Windows; clear the attribute before deleting.
        foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_tempDir, recursive: true);
    }

    private static void Git(string workingDir, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit();
    }

    private string InitRepoWithCommitAndBareRemote()
    {
        Git(_tempDir, "init");
        Git(_tempDir, "config", "user.email", "test@example.com");
        Git(_tempDir, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        Git(_tempDir, "add", ".");
        Git(_tempDir, "commit", "-m", "Initial commit");

        var barePath = Path.Combine(_tempDir, "remote.git");
        Directory.CreateDirectory(barePath);
        Git(barePath, "init", "--bare");
        Git(_tempDir, "remote", "add", "origin", barePath);
        Git(_tempDir, "push", "-u", "origin", "HEAD");
        return barePath;
    }

    [Fact]
    public async Task PushAsync_ReturnsSuccess_WhenUpstreamConfiguredAndNothingToPush()
    {
        // Arrange
        InitRepoWithCommitAndBareRemote();
        var sut = new GitCli();

        // Act
        var result = await sut.PushAsync(_tempDir);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_ReturnsSuccess_WhenNewCommitPushedToBareRemote()
    {
        // Arrange
        InitRepoWithCommitAndBareRemote();
        File.WriteAllText(Path.Combine(_tempDir, "extra.txt"), "extra");
        Git(_tempDir, "add", ".");
        Git(_tempDir, "commit", "-m", "Another commit");
        var sut = new GitCli();

        // Act
        var result = await sut.PushAsync(_tempDir);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PushAsync_ReturnsFailure_WithStderrMessage_WhenNoUpstreamConfigured()
    {
        // Arrange
        Git(_tempDir, "init");
        Git(_tempDir, "config", "user.email", "test@example.com");
        Git(_tempDir, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");
        Git(_tempDir, "add", ".");
        Git(_tempDir, "commit", "-m", "Initial commit");
        var sut = new GitCli();

        // Act
        var result = await sut.PushAsync(_tempDir);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PushAsync_ReturnsFailure_WhenDirectoryIsNotRepo()
    {
        // Arrange
        var sut = new GitCli();

        // Act
        var result = await sut.PushAsync(_tempDir);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }
}

using Bishop.App.Git;
using FluentAssertions;

namespace Bishop.Tests.App.Git;

public sealed class PushNewBranchAsyncTests : IDisposable
{
    private readonly string _tempDir;

    public PushNewBranchAsyncTests()
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

    private static string GitOutput(string workingDir, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return output.Trim();
    }

    private string InitRepoWithNewBranchAndBareRemote(string branchName)
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
        Git(_tempDir, "push", "origin", "HEAD");

        Git(_tempDir, "checkout", "-b", branchName);
        File.WriteAllText(Path.Combine(_tempDir, "feature.txt"), "feature");
        Git(_tempDir, "add", ".");
        Git(_tempDir, "commit", "-m", "Feature commit");

        return barePath;
    }

    [Fact]
    public async Task PushNewBranchAsync_Succeeds_WhenNoBranchUpstreamConfigured()
    {
        // Arrange
        const string branchName = "feature/test-branch";
        InitRepoWithNewBranchAndBareRemote(branchName);
        var sut = new GitCli();

        // Act
        var result = await sut.PushNewBranchAsync(_tempDir, branchName);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PushNewBranchAsync_SetsUpstreamTracking_AfterPush()
    {
        // Arrange
        const string branchName = "feature/test-branch";
        InitRepoWithNewBranchAndBareRemote(branchName);
        var sut = new GitCli();

        // Act
        await sut.PushNewBranchAsync(_tempDir, branchName);

        // Assert
        var upstream = GitOutput(_tempDir, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
        upstream.Should().Be($"origin/{branchName}");
    }
}

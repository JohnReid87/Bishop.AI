using Bishop.App.Services.GitHub;
using FluentAssertions;

namespace Bishop.Tests.App.Services.GitHub;

public sealed class GhCliExceptionTests
{
    [Fact]
    public void Message_IsGeneric_DoesNotContainRawStderr()
    {
        var sensitiveStderr = "Bearer ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ123456";

        var sut = new GhCliException(exitCode: 1, stderr: sensitiveStderr);

        sut.Message.Should().NotContain("Bearer ");
        sut.Message.Should().NotContain("ghp_");
    }

    [Theory]
    [InlineData("Bearer ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ123456")]
    [InlineData("error: oauth2: cannot fetch token: 401 Unauthorized\nBearer gho_secret123")]
    [InlineData("remote: Invalid username or password.\nghp_ABCDE1234567890ABCDE1234567890")]
    public void Message_NeverContainsTokenShapedContent(string sensitiveStderr)
    {
        var sut = new GhCliException(exitCode: 1, stderr: sensitiveStderr);

        sut.Message.Should().NotMatchRegex(@"gh[pous]_[A-Za-z0-9]+");
        sut.Message.Should().NotContain("Bearer ");
    }

    [Fact]
    public void Message_ContainsExitCode()
    {
        var sut = new GhCliException(exitCode: 128, stderr: "some error");

        sut.Message.Should().Contain("128");
    }

    [Fact]
    public void Stderr_ExposesRawStderr()
    {
        const string rawStderr = "Bearer ghp_secret";

        var sut = new GhCliException(exitCode: 1, stderr: rawStderr);

        sut.Stderr.Should().Be(rawStderr);
    }

    [Fact]
    public void ExitCode_IsPreserved()
    {
        var sut = new GhCliException(exitCode: 42, stderr: "");

        sut.ExitCode.Should().Be(42);
    }
}

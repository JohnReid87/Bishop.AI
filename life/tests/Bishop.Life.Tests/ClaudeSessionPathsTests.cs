using Bishop.Life.Core;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class ClaudeSessionPathsTests
{
    [Theory]
    [InlineData(@"C:\Users\johne\source\repos\Bishop.AI", "C--Users-johne-source-repos-Bishop-AI")]
    [InlineData(@"C:\Users\johne\AppData\Roaming\Bishop\life", "C--Users-johne-AppData-Roaming-Bishop-life")]
    [InlineData(@"D:/x/y.z", "D--x-y-z")]
    [InlineData("", "")]
    public void EncodeCwd_ReplacesSeparatorFamilyWithDash(string cwd, string expected)
    {
        ClaudeSessionPaths.EncodeCwd(cwd).Should().Be(expected);
    }

    [Fact]
    public void ResolveSessionFilePath_BuildsExpectedShape()
    {
        var path = ClaudeSessionPaths.ResolveSessionFilePath(
            @"C:\Users\johne\source\repos\Bishop.AI",
            "abcd-1234");

        path.Should().EndWith(Path.Combine(
            ".claude", "projects", "C--Users-johne-source-repos-Bishop-AI", "abcd-1234.jsonl"));
    }
}

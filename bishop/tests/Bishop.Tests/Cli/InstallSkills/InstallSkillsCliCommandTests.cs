using Bishop.Cli.InstallSkills;
using FluentAssertions;
using System.CommandLine;

namespace Bishop.Tests.Cli.InstallSkills;

public sealed class InstallSkillsCliCommandTests
{
    [Fact]
    public async Task InvokeAsync_HappyPath_ExitsZeroAndCopiesSkillFiles()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var skillDir = Path.Combine(sourceDir, "my-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# test skill");

            var cmd = new InstallSkillsCliCommand(sourceDir, destRoot);
            var exitCode = await cmd.InvokeAsync([]);

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(destRoot, "my-skill", "SKILL.md")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(destRoot)) Directory.Delete(destRoot, recursive: true);
        }
    }
}

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

    [Fact]
    public async Task InvokeAsync_OrphanedBishSkillInDest_IsRemoved()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var skillDir = Path.Combine(sourceDir, "bish-work-on-card");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# test skill");

            var orphanDir = Path.Combine(destRoot, "bish-chat");
            Directory.CreateDirectory(orphanDir);
            File.WriteAllText(Path.Combine(orphanDir, "SKILL.md"), "# removed skill");

            var cmd = new InstallSkillsCliCommand(sourceDir, destRoot);
            var exitCode = await cmd.InvokeAsync([]);

            exitCode.Should().Be(0);
            Directory.Exists(orphanDir).Should().BeFalse();
            File.Exists(Path.Combine(destRoot, "bish-work-on-card", "SKILL.md")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(destRoot)) Directory.Delete(destRoot, recursive: true);
        }
    }

    [Fact]
    public async Task InvokeAsync_NonBishSkillInDest_IsLeftUntouched()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var skillDir = Path.Combine(sourceDir, "bish-work-on-card");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# test skill");

            var userSkillDir = Path.Combine(destRoot, "my-own-skill");
            Directory.CreateDirectory(userSkillDir);
            File.WriteAllText(Path.Combine(userSkillDir, "SKILL.md"), "# user's skill");

            var cmd = new InstallSkillsCliCommand(sourceDir, destRoot);
            var exitCode = await cmd.InvokeAsync([]);

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(userSkillDir, "SKILL.md")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(destRoot)) Directory.Delete(destRoot, recursive: true);
        }
    }
}

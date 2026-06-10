using System.CommandLine;

namespace Bishop.Cli.InstallSkills;

internal sealed class InstallSkillsCliCommand : Command
{
    public InstallSkillsCliCommand(string? sourceDirOverride = null, string? destRootOverride = null)
        : base("install-skills", "Copy bundled skills to ~/.claude/skills/ (overwrites existing).")
    {
        this.SetHandler(() =>
        {
            var sourceDir = sourceDirOverride ?? Path.Combine(AppContext.BaseDirectory, "skills");
            if (!Directory.Exists(sourceDir))
            {
                Console.Error.WriteLine($"No skills/ directory bundled with bishop (expected at {sourceDir}).");
                Environment.ExitCode = 1;
                return;
            }

            var destRoot = destRootOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude",
                "skills");
            Directory.CreateDirectory(destRoot);

            var installed = 0;
            foreach (var skillSourceDir in Directory.GetDirectories(sourceDir))
            {
                var name = Path.GetFileName(skillSourceDir);
                var sourceFiles = Directory.GetFiles(skillSourceDir, "*", SearchOption.AllDirectories);
                if (sourceFiles.Length == 0)
                {
                    // Empty husk left in bin/ output by MSBuild after a skill rename — content-copy
                    // semantics don't delete files removed from source. Skip silently rather than
                    // print a misleading "Installed" line for a directory with nothing in it.
                    continue;
                }

                var skillDestDir = Path.Combine(destRoot, name);
                foreach (var file in sourceFiles)
                {
                    var relative = Path.GetRelativePath(skillSourceDir, file);
                    var destFile = Path.Combine(skillDestDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(file, destFile, overwrite: true);
                }
                Console.WriteLine($"Installed skill '{name}' to {skillDestDir}");
                installed++;
            }

            if (installed == 0)
            {
                Console.WriteLine("No skills found to install.");
            }
        });
    }
}

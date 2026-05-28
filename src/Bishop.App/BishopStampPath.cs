namespace Bishop.App;

public static class BishopStampPath
{
    public static string Resolve()
    {
        var envOverride = Environment.GetEnvironmentVariable("BISHOP_STAMP");
        if (!string.IsNullOrEmpty(envOverride))
        {
            var resolved = Path.GetFullPath(envOverride);
            AssertWithinUserProfile("BISHOP_STAMP", resolved);
            var envDir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(envDir))
                Directory.CreateDirectory(envDir);
            return resolved;
        }

        var appData = Environment.GetEnvironmentVariable("APPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Bishop.AI");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "migration_stamp");
    }

    private static void AssertWithinUserProfile(string varName, string resolvedPath)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var prefix = userProfile + Path.DirectorySeparatorChar;
        if (!resolvedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedPath, userProfile, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Environment variable {varName} resolves to '{resolvedPath}', which is outside the allowed directory '{userProfile}'.");
        }
    }
}

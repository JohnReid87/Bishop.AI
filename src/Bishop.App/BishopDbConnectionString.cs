namespace Bishop.App;

public static class BishopDbConnectionString
{
    public static string Resolve()
    {
        var envOverride = Environment.GetEnvironmentVariable("BISHOP_DB");
        if (!string.IsNullOrEmpty(envOverride))
        {
            var resolved = Path.GetFullPath(envOverride);
            AssertWithinUserProfile("BISHOP_DB", resolved);
            return $"Data Source={resolved}";
        }

        var appData = Environment.GetEnvironmentVariable("APPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Bishop.AI");
        Directory.CreateDirectory(dir);
        return $"Data Source={Path.Combine(dir, "bishop.db")}";
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

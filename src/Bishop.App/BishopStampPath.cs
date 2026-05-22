namespace Bishop.App;

public static class BishopStampPath
{
    public static string Resolve()
    {
        var envOverride = Environment.GetEnvironmentVariable("BISHOP_STAMP");
        if (!string.IsNullOrEmpty(envOverride))
        {
            var envDir = Path.GetDirectoryName(envOverride);
            if (!string.IsNullOrEmpty(envDir))
                Directory.CreateDirectory(envDir);
            return envOverride;
        }

        var appData = Environment.GetEnvironmentVariable("APPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Bishop.AI");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "migration_stamp");
    }
}

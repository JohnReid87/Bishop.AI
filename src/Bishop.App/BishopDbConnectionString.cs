namespace Bishop.App;

public static class BishopDbConnectionString
{
    public static string Resolve()
    {
        var envOverride = Environment.GetEnvironmentVariable("BISHOP_DB");
        if (!string.IsNullOrEmpty(envOverride))
            return $"Data Source={envOverride}";

        var appData = Environment.GetEnvironmentVariable("APPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Bishop.AI");
        Directory.CreateDirectory(dir);
        return $"Data Source={Path.Combine(dir, "bishop.db")}";
    }
}

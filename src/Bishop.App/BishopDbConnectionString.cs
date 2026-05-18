namespace Bishop.App;

public static class BishopDbConnectionString
{
    public static string Resolve()
    {
        var envOverride = Environment.GetEnvironmentVariable("BISHOP_DB");
        if (!string.IsNullOrEmpty(envOverride))
            return $"Data Source={envOverride}";

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI");
        Directory.CreateDirectory(dir);
        return $"Data Source={Path.Combine(dir, "bishop.db")}";
    }
}

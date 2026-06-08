namespace Bishop.Life.Core;

public static class LifePlanPaths
{
    public const string EnvVarName = "BISHOP_LIFE_FILE";

    public const string FileName = "bishop.life.json";
    public const string TempSuffix = ".tmp";
    public const string PrevSuffix = ".prev";

    public static string Resolve()
    {
        var overridePath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!Path.IsPathFullyQualified(overridePath))
                throw new InvalidOperationException(
                    $"{EnvVarName} must be an absolute path. Got: {overridePath}");
            return overridePath;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Bishop", "life", FileName);
    }

    public static string TempPathFor(string filePath) => filePath + TempSuffix;
    public static string PrevPathFor(string filePath) => filePath + PrevSuffix;
}

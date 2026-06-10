namespace Bishop.Life.Core;

public static class LifePlanPaths
{
    public const string EnvVarName = "BISHOP_LIFE_FILE";

    public const string FileName = "bishop.life.json";
    public const string GoogleTokenFileName = "google-token.json";
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

    /// <summary>
    /// Resolves the path to the DPAPI-encrypted Google OAuth refresh-token file. Sits in the
    /// same directory as <see cref="Resolve"/>, so the <see cref="EnvVarName"/> override naturally
    /// keeps tokens out of the per-machine <c>%APPDATA%</c> when the user redirects the life file.
    /// </summary>
    public static string ResolveGoogleTokenPath()
    {
        var lifeFile = Resolve();
        var directory = Path.GetDirectoryName(lifeFile)
            ?? throw new InvalidOperationException($"Could not resolve directory for life file: {lifeFile}");
        return Path.Combine(directory, GoogleTokenFileName);
    }
}

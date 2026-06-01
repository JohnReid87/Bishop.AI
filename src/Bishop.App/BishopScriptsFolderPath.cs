namespace Bishop.App;

public static class BishopScriptsFolderPath
{
    public static string Resolve() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI", "scripts");
}

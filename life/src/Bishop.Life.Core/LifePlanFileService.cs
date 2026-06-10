using Bishop.Life.Core.Schema;

namespace Bishop.Life.Core;

public sealed class LifePlanFileService
{
    private readonly string _filePath;

    public LifePlanFileService() : this(LifePlanPaths.Resolve()) { }

    public LifePlanFileService(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;
    public string TempPath => LifePlanPaths.TempPathFor(_filePath);
    public string PrevPath => LifePlanPaths.PrevPathFor(_filePath);

    public bool Exists() => File.Exists(_filePath);

    public LifePlan Load()
    {
        var json = File.ReadAllText(_filePath);
        return LifePlanJson.Deserialize(json);
    }

    /// <summary>
    /// Atomic save: snapshot current file to .prev, write JSON to .tmp, then rename .tmp to target.
    /// A process kill at any point leaves either the original or the new file intact, never partial.
    /// </summary>
    public void Save(LifePlan plan)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(_filePath))
            File.Copy(_filePath, PrevPath, overwrite: true);

        var json = LifePlanJson.Serialize(plan);
        File.WriteAllText(TempPath, json);

        if (File.Exists(_filePath))
            File.Replace(TempPath, _filePath, destinationBackupFileName: null);
        else
            File.Move(TempPath, _filePath);
    }
}

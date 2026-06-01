using MediatR;

namespace Bishop.App.Scripts.GetScripts;

internal sealed class GetScriptsQueryHandler : IRequestHandler<GetScriptsQuery, IReadOnlyList<ScriptInfo>>
{
    private readonly string _scriptsFolder;

    public GetScriptsQueryHandler()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bishop.AI", "scripts"))
    { }

    internal GetScriptsQueryHandler(string scriptsFolder) => _scriptsFolder = scriptsFolder;

    public Task<IReadOnlyList<ScriptInfo>> Handle(GetScriptsQuery request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_scriptsFolder);

        var scripts = Directory.EnumerateFiles(_scriptsFolder, "*.ps1")
            .Select(path => new ScriptInfo(Path.GetFileNameWithoutExtension(path), path))
            .OrderBy(s => s.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<ScriptInfo>>(scripts);
    }
}

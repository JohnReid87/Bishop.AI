using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using MediatR;

namespace Bishop.App.Scripts.LaunchScript;

internal sealed class LaunchScriptCommandHandler : IRequestHandler<LaunchScriptCommand, bool>
{
    private readonly ITerminalLauncher _launcher;

    public LaunchScriptCommandHandler(ITerminalLauncher launcher) => _launcher = launcher;

    public Task<bool> Handle(LaunchScriptCommand request, CancellationToken cancellationToken)
    {
        var canonicalScriptPath = Path.GetFullPath(request.ScriptPath);
        var scriptDir = Path.GetDirectoryName(canonicalScriptPath)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var args = new List<string> { "-File", canonicalScriptPath };
        if (!string.IsNullOrWhiteSpace(request.Args))
            args.AddRange(SplitArgs(request.Args));

        return Task.FromResult(_launcher.LaunchCommand(scriptDir, "pwsh.exe", [.. args], null));
    }

    // Splits a raw argument string into tokens, respecting single- and double-quoted spans so
    // a path like "C:\My Docs\data.csv" or a value like 'hello world' arrives as one token.
    private static IEnumerable<string> SplitArgs(string args)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';

        foreach (var c in args)
        {
            if (inQuote)
            {
                if (c == quoteChar)
                    inQuote = false;
                else
                    current.Append(c);
            }
            else if (c is '"' or '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == ' ')
            {
                if (current.Length > 0)
                {
                    var token = SkillCommandRenderer.Sanitize(current.ToString());
                    if (token.Length > 0) tokens.Add(token);
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            var last = SkillCommandRenderer.Sanitize(current.ToString());
            if (last.Length > 0) tokens.Add(last);
        }

        return tokens;
    }
}

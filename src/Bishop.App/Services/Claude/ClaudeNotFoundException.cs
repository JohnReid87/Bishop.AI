namespace Bishop.App.Services.Claude;

public sealed class ClaudeNotFoundException : Exception
{
    public ClaudeNotFoundException(IReadOnlyList<string> candidates, IReadOnlyList<string> directories)
        : base("Could not find 'claude' on PATH.")
    {
        Candidates = candidates;
        Directories = directories;
    }

    public IReadOnlyList<string> Candidates { get; }

    public IReadOnlyList<string> Directories { get; }
}

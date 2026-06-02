namespace Bishop.App.Services.GitHub;

public sealed class GhCliException : Exception
{
    public GhCliException(int exitCode, string stderr)
        : base($"gh failed with exit code {exitCode}.")
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    public int ExitCode { get; }

    /// <summary>
    /// Raw stderr from gh. May contain credential material — log at Debug only.
    /// </summary>
    public string Stderr { get; }
}

using System.CommandLine;

namespace Bishop.Cli;

internal static class CommonOptions
{
    public static readonly Option<string?> WorkspaceOption = new(
        aliases: ["--workspace", "-w"],
        description: "Workspace name or path (defaults to CWD ancestor match)");

    public static readonly Option<bool> JsonOption = new(
        name: "--json",
        description: "Emit JSON output");
}

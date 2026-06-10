namespace Bishop.App.Skills;

public static class SkillCommandRenderer
{
    public static string Render(string template, int? cardNumber, string? cardTitle, string? cardDescription, string workspacePath) =>
        template
            .Replace("{{workspace_path}}", workspacePath)
            .Replace("{{card_number}}", cardNumber?.ToString() ?? string.Empty)
            .Replace("{{card_title}}", Sanitize(cardTitle ?? string.Empty))
            .Replace("{{card_description}}", Sanitize(cardDescription ?? string.Empty));

    // Strip cmd.exe /k shell metacharacters from user-supplied text so & | < > ^
    // or newlines cannot inject extra commands when the string reaches cmd.exe /k.
    // internal so BoardSkillsCoordinator and LaunchScriptCommandHandler can sanitize
    // stagedText / SplitArgs tokens at their respective entry points.
    internal static string Sanitize(string value) =>
        value
            .Replace("&", string.Empty)
            .Replace("|", string.Empty)
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace("^", string.Empty)
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ');
}

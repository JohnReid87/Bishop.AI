namespace Bishop.App.Skills;

public static class SkillCommandRenderer
{
    public static string Render(string template, int? cardNumber, string? cardTitle, string? cardDescription, string workspacePath) =>
        template
            .Replace("{{workspace_path}}", workspacePath)
            .Replace("{{card_number}}", cardNumber?.ToString() ?? string.Empty)
            .Replace("{{card_title}}", Sanitize(cardTitle ?? string.Empty))
            .Replace("{{card_description}}", Sanitize(cardDescription ?? string.Empty));

    // Strip cmd.exe /k shell metacharacters so card title/description containing & | < > ^
    // or newlines cannot inject extra commands when the rendered string is passed as a single
    // /k argument to cmd.exe.
    private static string Sanitize(string value) =>
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

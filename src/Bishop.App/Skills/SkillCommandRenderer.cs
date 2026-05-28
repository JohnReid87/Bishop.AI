namespace Bishop.App.Skills;

public static class SkillCommandRenderer
{
    public static string Render(string template, int? cardNumber, string? cardTitle, string? cardDescription, string workspacePath) =>
        template
            .Replace("{{workspace_path}}", workspacePath)
            .Replace("{{card_number}}", cardNumber?.ToString() ?? string.Empty)
            .Replace("{{card_title}}", cardTitle ?? string.Empty)
            .Replace("{{card_description}}", cardDescription ?? string.Empty);
}

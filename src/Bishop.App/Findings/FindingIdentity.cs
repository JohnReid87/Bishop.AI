using System.Security.Cryptography;
using System.Text;

namespace Bishop.App.Findings;

internal static class FindingIdentity
{
    public static string Compute(string skillName, string? projectName, string? file, string? rule, string? symbol, string title)
    {
        var input = HasAllStructuredInputs(file, rule, symbol)
            ? string.Concat(skillName, projectName ?? string.Empty, file, rule, symbol)
            : string.Concat(skillName, projectName ?? string.Empty, title);

        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool HasAllStructuredInputs(string? file, string? rule, string? symbol) =>
        !string.IsNullOrEmpty(file) && !string.IsNullOrEmpty(rule) && !string.IsNullOrEmpty(symbol);
}

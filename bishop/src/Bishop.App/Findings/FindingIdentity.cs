using System.Security.Cryptography;
using System.Text;

namespace Bishop.App.Findings;

internal static class FindingIdentity
{
    public static string Compute(string skillName, string? projectName, string? file, string title)
    {
        var input = !string.IsNullOrEmpty(file)
            ? string.Concat(skillName, projectName ?? string.Empty, file, title)
            : string.Concat(skillName, projectName ?? string.Empty, title);

        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

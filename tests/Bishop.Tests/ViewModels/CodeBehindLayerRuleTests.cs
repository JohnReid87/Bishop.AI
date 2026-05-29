using System.Text.RegularExpressions;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

/// <summary>
/// Enforces the thin-code-behind rule documented in CONTEXT.md: code-behind
/// (<c>*.xaml.cs</c>) under <c>src/Bishop.UI</c> is limited to view mechanics and
/// must not reference <c>Bishop.App</c> — all application calls and state go
/// through the ViewModel. A source-text scan is used (not reflection) because
/// Bishop.UI legitimately references Bishop.App via its composition root, so an
/// assembly-level reference test can't work; the scan matches the convention's
/// wording exactly with no new build infrastructure.
/// </summary>
public class CodeBehindLayerRuleTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    // Matches both `using Bishop.App...` directives and fully-qualified
    // `Bishop.App.` usages. The trailing word boundary keeps it from matching
    // unrelated identifiers such as `Bishop.AppHost`.
    private static readonly Regex BishopAppReference =
        new(@"Bishop\.App\b", RegexOptions.Compiled);

    // Files exempt from the rule, keyed by file name. App.xaml.cs is the sole
    // permanent exception (the DI composition root); the rest are current
    // offenders whose remediation is tracked by the noted card and must be
    // removed from this allowlist once conformed.
    private static readonly IReadOnlyDictionary<string, string> Allowlist =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["App.xaml.cs"] = "DI composition root — sanctioned Bishop.App reference",
        };

    [Fact]
    public void UiCodeBehind_DoesNotReferenceBishopApp()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "Bishop.UI");
        Directory.Exists(uiRoot)
            .Should().BeTrue($"expected Bishop.UI source under {uiRoot}");

        var violations = Directory
            .EnumerateFiles(uiRoot, "*.xaml.cs", SearchOption.AllDirectories)
            .Where(file => !Allowlist.ContainsKey(Path.GetFileName(file)))
            .Where(file => BishopAppReference.IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileName)
            .ToList();

        violations
            .Should()
            .BeEmpty("code-behind (*.xaml.cs) must not reference Bishop.App — route application " +
                "calls and state through the ViewModel (see the MVVM rule in CONTEXT.md). Add a file " +
                "to the allowlist only as a temporary exception annotated with its remediation card");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any())
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the solution file walking up from {AppContext.BaseDirectory}.");
    }
}

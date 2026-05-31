using System.Text.RegularExpressions;
using FluentAssertions;

namespace Bishop.Tests.Architecture;

/// <summary>
/// Guards <c>App.Services</c> from drifting into an ambient service-locator. The
/// static <c>IServiceProvider</c> on <c>App</c> exists so XAML-instantiated views
/// (whose parameterless ctors are constructed by WinUI reflection) can fetch
/// their ViewModels and services at the composition boundary. Any new
/// <c>App.Services.GetRequiredService</c> call in code-behind must be added to
/// the allowlist below — making the drift explicit at review time — or routed
/// through the ViewModel instead.
/// </summary>
public class AppServicesLocatorRuleTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly Regex AppServicesReference =
        new(@"App\.Services\.GetRequiredService\b", RegexOptions.Compiled);

    // Files that may use App.Services.GetRequiredService, keyed by file name.
    // All current entries are XAML-instantiated views or dialogs that pull
    // their dependencies at the composition boundary because WinUI requires a
    // parameterless ctor. Add a file here only when no ViewModel-routed
    // alternative exists, and review additions deliberately.
    private static readonly IReadOnlyDictionary<string, string> Allowlist =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WorkspaceDetailPage.xaml.cs"] = "XAML-instantiated page — parameterless ctor pulls VMs at composition boundary",
            ["MainWindow.xaml.cs"] = "XAML-instantiated window — parameterless ctor pulls services at composition boundary",
            ["CardDetailDialog.xaml.cs"] = "XAML-instantiated dialog — parameterless ctor pulls services at composition boundary",
            ["FindingsPage.xaml.cs"] = "XAML-instantiated page — parameterless ctor pulls VM at composition boundary",
            ["ScriptsPage.xaml.cs"] = "XAML-instantiated page — parameterless ctor pulls VM at composition boundary",
            ["PushLaneToGitHubDialog.xaml.cs"] = "XAML-instantiated dialog — parameterless ctor pulls services at composition boundary",
            ["ImportFromGitHubDialog.xaml.cs"] = "XAML-instantiated dialog — parameterless ctor pulls services at composition boundary",
            ["SettingsDialog.xaml.cs"] = "XAML-instantiated dialog — parameterless ctor pulls services at composition boundary",
            ["AddWorkspaceDialog.xaml.cs"] = "XAML-instantiated dialog — parameterless ctor pulls services at composition boundary",
            ["ManageWorkspacesControl.xaml.cs"] = "XAML-instantiated control — parameterless ctor pulls VM at composition boundary",
        };

    [Fact]
    public void UiCodeBehind_DoesNotServiceLocateThroughAppServices()
    {
        var uiRoot = Path.Combine(RepoRoot, "src", "Bishop.UI");
        Directory.Exists(uiRoot)
            .Should().BeTrue($"expected Bishop.UI source under {uiRoot}");

        var violations = Directory
            .EnumerateFiles(uiRoot, "*.xaml.cs", SearchOption.AllDirectories)
            .Where(file => !Allowlist.ContainsKey(Path.GetFileName(file)))
            .Where(file => AppServicesReference.IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileName)
            .ToList();

        violations
            .Should()
            .BeEmpty("App.Services is reserved for XAML-instantiated views resolving dependencies at their " +
                "composition boundary — route other code-behind through the ViewModel instead. Add a file to " +
                "the allowlist only when a parameterless ctor genuinely requires service-locator access");
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

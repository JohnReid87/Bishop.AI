using System.Reflection;
using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

/// <summary>
/// Enforces the MVVM layer rule documented in CONTEXT.md: Bishop.ViewModels must
/// remain free of WinUI / Windows App SDK references so its ViewModels stay
/// presentation-framework-agnostic and unit-testable.
/// </summary>
public class BishopViewModelsLayerRuleTests
{
    private static readonly Assembly ViewModelsAssembly = typeof(IUiDispatcher).Assembly;

    [Theory]
    [InlineData("Microsoft.UI")]
    [InlineData("Microsoft.UI.Xaml")]
    [InlineData("Microsoft.UI.Dispatching")]
    [InlineData("Microsoft.WindowsAppSDK")]
    [InlineData("Microsoft.Windows.SDK.NET")]
    [InlineData("WinRT.Runtime")]
    public void ViewModelsAssembly_DoesNotReferenceUiFrameworkAssembly(string forbiddenName)
    {
        var referenced = ViewModelsAssembly.GetReferencedAssemblies();

        referenced
            .Should()
            .NotContain(a => a.Name == forbiddenName,
                $"Bishop.ViewModels must not depend on {forbiddenName} — the layer rule keeps VMs presentation-framework-agnostic");
    }

    [Fact]
    public void IUiDispatcher_LivesInBishopViewModels()
    {
        typeof(IUiDispatcher).Assembly.GetName().Name
            .Should().Be("Bishop.ViewModels");
    }
}

using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class ImportFromGitHubDialogViewModelTests
{
    private static ImportFromGitHubDialogViewModel Make()
        => new(Guid.Empty, string.Empty, null!, null!);

    [Fact]
    public void Defaults_AreSane()
    {
        var vm = Make();

        vm.Labels.Should().ContainSingle().Which.Should().Be("(any)");
        vm.SelectedLabel.Should().Be("(any)");
        vm.LabelFilter.Should().BeNull();
        vm.LimitText.Should().Be("100");
        vm.Limit.Should().Be(100);
        vm.IsBusy.Should().BeFalse();
        vm.IsIdle.Should().BeTrue();
    }

    [Fact]
    public void IsBusy_TogglingFlipsIsIdle()
    {
        var vm = Make();

        vm.IsBusy = true;

        vm.IsIdle.Should().BeFalse();
    }

    [Fact]
    public void LabelFilter_NullForAnySentinel_OtherwiseSelectedLabel()
    {
        var vm = Make();

        vm.SelectedLabel = "(any)";
        vm.LabelFilter.Should().BeNull();

        vm.SelectedLabel = "bug";
        vm.LabelFilter.Should().Be("bug");
    }

    [Fact]
    public void Limit_FallsBackTo100ForInvalidText()
    {
        var vm = Make();
        vm.LimitText = "not a number";

        vm.Limit.Should().Be(100);
    }
}

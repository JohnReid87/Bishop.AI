using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class WorkNextOptionsDialogViewModelTests
{
    [Fact]
    public void Constructor_SeedsTagsWithAnySentinelFirst()
    {
        var vm = new WorkNextOptionsDialogViewModel(["bug", "feature"]);

        vm.Tags.Should().HaveCount(3);
        vm.Tags[0].Should().Be(WorkNextOptionsDialogViewModel.AnyTagSentinel);
        vm.Tags.Should().Contain(["bug", "feature"]);
    }

    [Fact]
    public void Constructor_PicksLastModelIdWhenKnown()
    {
        var vm = new WorkNextOptionsDialogViewModel([], lastModelId: "claude-opus-4-7");

        vm.SelectedModel.Id.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public void Constructor_FallsBackToSonnetForUnknownLastModelId()
    {
        var vm = new WorkNextOptionsDialogViewModel([], lastModelId: "claude-nope");

        vm.SelectedModel.Should().Be(WorkNextOptionsDialogViewModel.Models[1]);
    }

    [Fact]
    public void SelectedTagOrNull_NullForAnySentinel()
    {
        var vm = new WorkNextOptionsDialogViewModel([]);

        vm.SelectedTag = WorkNextOptionsDialogViewModel.AnyTagSentinel;
        vm.SelectedTagOrNull.Should().BeNull();

        vm.SelectedTag = "bug";
        vm.SelectedTagOrNull.Should().Be("bug");
    }

    [Theory]
    [InlineData("10", true, 10)]
    [InlineData("0", true, 0)]
    [InlineData("-1", false, 0)]
    [InlineData("abc", false, 0)]
    public void CanConfirm_RequiresNonNegativeInteger(string maxText, bool canConfirm, int maxValue)
    {
        var vm = new WorkNextOptionsDialogViewModel([]) { MaxText = maxText };

        vm.CanConfirm.Should().Be(canConfirm);
        vm.MaxValue.Should().Be(maxValue);
    }
}

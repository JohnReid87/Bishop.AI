using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class BatchGroupViewModelTests
{
    [Fact]
    public void IsExpanded_DefaultsToTrue()
    {
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid() };

        vm.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void ToggleExpandedCommand_TogglesIsExpanded()
    {
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid() };

        vm.ToggleExpandedCommand.Execute(null);
        vm.IsExpanded.Should().BeFalse();

        vm.ToggleExpandedCommand.Execute(null);
        vm.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void AccentColor_DefaultsToSlot0_SignalGreen()
    {
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid() };

        vm.AccentColor.Should().Be("#00FF41");
    }

    [Fact]
    public void AccentColor_IsOneOfSixPaletteSlots()
    {
        string[] palette = ["#00FF41", "#BF40FF", "#1A8CFF", "#FF1493", "#00FFFF", "#FF6B00"];
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid(), AccentIndex = 3 };

        palette.Should().Contain(vm.AccentColor);
    }

    [Theory]
    [InlineData(0, "#00FF41")]
    [InlineData(1, "#BF40FF")]
    [InlineData(2, "#1A8CFF")]
    [InlineData(3, "#FF1493")]
    [InlineData(4, "#00FFFF")]
    [InlineData(5, "#FF6B00")]
    public void AccentColor_ReturnsCorrectColorForEachSlot(int index, string expected)
    {
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid(), AccentIndex = index };

        vm.AccentColor.Should().Be(expected);
    }

    [Fact]
    public void AccentColor_WrapsAround_WhenAccentIndexExceedsPaletteSize()
    {
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid(), AccentIndex = 6 };

        vm.AccentColor.Should().Be("#00FF41");
    }

    [Fact]
    public void AccentColor_RaisesPropertyChanged_WhenAccentIndexChanges()
    {
        var vm = new BatchGroupViewModel { BatchId = Guid.NewGuid() };
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.AccentIndex = 2;

        changed.Should().Contain(nameof(BatchGroupViewModel.AccentColor));
    }
}

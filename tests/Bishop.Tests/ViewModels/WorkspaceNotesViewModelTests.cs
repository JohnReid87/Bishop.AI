using Bishop.ViewModels;
using FluentAssertions;

namespace Bishop.Tests.ViewModels;

public class WorkspaceNotesViewModelTests
{
    [Fact]
    public void IsExpanded_TogglingRaisesChevronGlyphChange()
    {
        var vm = NewVm();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsExpanded = true;

        changed.Should().Contain(nameof(WorkspaceNotesViewModel.ChevronGlyph));
    }

    [Fact]
    public void SaveStatusOpacity_DimsWhileEditing()
    {
        var vm = NewVm();

        vm.SaveStatusIsEditing = false;
        vm.SaveStatusOpacity.Should().Be(1.0);

        vm.SaveStatusIsEditing = true;
        vm.SaveStatusOpacity.Should().Be(0.5);
    }

    [Fact]
    public void Defaults_AreCollapsedAndIdle()
    {
        var vm = NewVm();

        vm.IsExpanded.Should().BeFalse();
        vm.IsExternalChangeBarVisible.Should().BeFalse();
        vm.SaveStatusIsEditing.Should().BeFalse();
        vm.SaveStatusIsError.Should().BeFalse();
        vm.NotesContent.Should().BeEmpty();
        vm.PanelHeight.Should().Be(200);
    }

    private static WorkspaceNotesViewModel NewVm() =>
        new(new FakeUiDispatcher());

    private sealed class FakeUiDispatcher : IUiDispatcher
    {
        public void TryEnqueue(Action work) => work();
        public void TryEnqueue(Func<Task> work) => _ = work();
    }
}

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

    [Fact]
    public void ChevronGlyph_IsAccessibleForBothExpandedStates()
    {
        var vm = NewVm();

        vm.IsExpanded = false;
        vm.ChevronGlyph.Should().NotBeNull();

        vm.IsExpanded = true;
        vm.ChevronGlyph.Should().NotBeNull();
    }

    [Fact]
    public void KeepEditsCommand_HidesExternalChangeBar()
    {
        var vm = NewVm();
        vm.IsExternalChangeBarVisible = true;

        vm.KeepEditsCommand.Execute(null);

        vm.IsExternalChangeBarVisible.Should().BeFalse();
    }

    [Fact]
    public async Task QuickSaveAsync_WithNoWorkspacePath_ClearsExternalChangeBar()
    {
        var vm = NewVm();
        vm.IsExternalChangeBarVisible = true;

        await vm.QuickSaveAsync();

        vm.IsExternalChangeBarVisible.Should().BeFalse();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var vm = NewVm();
        var act = () => vm.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void NotesContent_WhenChanged_SetsEditingState()
    {
        var vm = NewVm();

        vm.NotesContent = "hello";

        vm.SaveStatusIsEditing.Should().BeTrue();
        vm.SaveStatusIsError.Should().BeFalse();
        vm.SaveStatusText.Should().Be("Editing…");
    }

    [Fact]
    public async Task ToggleCommand_WhenCollapsed_Expands()
    {
        var vm = NewVm();
        vm.IsExpanded = false;

        await vm.ToggleCommand.ExecuteAsync(null);

        vm.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleCommand_WhenExpanded_Collapses()
    {
        var vm = NewVm();
        vm.IsExpanded = true;

        await vm.ToggleCommand.ExecuteAsync(null);

        vm.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadCommand_HidesExternalChangeBar()
    {
        var vm = NewVm();
        vm.IsExternalChangeBarVisible = true;

        await vm.ReloadCommand.ExecuteAsync(null);

        vm.IsExternalChangeBarVisible.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadCommand_WithNoFile_LeavesContentEmpty()
    {
        var vm = NewVm();

        await vm.ReloadCommand.ExecuteAsync(null);

        vm.NotesContent.Should().BeEmpty();
    }

    [Fact]
    public async Task FlushAsync_WithNoWorkspacePath_DoesNotThrow()
    {
        var vm = NewVm();

        var act = async () => await vm.FlushAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task QuickSaveAsync_WhenSaveStatusIsError_KeepsExternalChangeBarVisible()
    {
        var vm = NewVm();
        vm.SaveStatusIsError = true;
        vm.IsExternalChangeBarVisible = true;

        await vm.QuickSaveAsync();

        vm.IsExternalChangeBarVisible.Should().BeTrue();
    }

    private static WorkspaceNotesViewModel NewVm() =>
        new(new FakeUiDispatcher());

    private sealed class FakeUiDispatcher : IUiDispatcher
    {
        public void TryEnqueue(Action work) => work();
        public void TryEnqueue(Func<Task> work) => _ = work();
    }
}

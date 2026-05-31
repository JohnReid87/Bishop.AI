using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Bishop.Tests.ViewModels.Workspaces;

[Collection("EnvVar")]
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
    public async Task Dispose_AfterLoadAsync_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            var act = () => vm.Dispose();
            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Dispose_CalledTwiceAfterLoadAsync_IsIdempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            vm.Dispose();
            var act = () => vm.Dispose();
            act.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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

    [Fact]
    public async Task LoadAsync_WithNewDirectory_CreatesNotesFileAndSetsIdleStatus()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);

            File.Exists(Path.Combine(dir, ".bishop", "BISHOP_NOTES.md")).Should().BeTrue();
            vm.NotesContent.Should().BeEmpty();
            vm.SaveStatusIsEditing.Should().BeFalse();
            vm.SaveStatusIsError.Should().BeFalse();
            vm.IsExternalChangeBarVisible.Should().BeFalse();
            vm.SaveStatusText.Should().StartWith("Saved");
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenNotesExist_ReadsExistingContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var bishopDir = Path.Combine(dir, ".bishop");
        Directory.CreateDirectory(bishopDir);
        await File.WriteAllTextAsync(Path.Combine(bishopDir, "BISHOP_NOTES.md"), "existing notes");
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);

            vm.NotesContent.Should().Be("existing notes");
            vm.SaveStatusIsEditing.Should().BeFalse();
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithNonExistentWorkspacePath_LeavesContentEmpty()
    {
        var vm = NewVm();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        await vm.LoadAsync(Guid.NewGuid(), nonExistentPath);

        vm.NotesContent.Should().BeEmpty();
        vm.SaveStatusIsEditing.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_SecondCall_FlushesUncommittedChangesToFirstWorkspace()
    {
        var dir1 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var dir2 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir1);
            vm.NotesContent = "unsaved changes";

            await vm.LoadAsync(Guid.NewGuid(), dir2);

            (await File.ReadAllTextAsync(Path.Combine(dir1, ".bishop", "BISHOP_NOTES.md")))
                .Should().Be("unsaved changes");
            vm.NotesContent.Should().BeEmpty();
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir1, recursive: true);
            Directory.Delete(dir2, recursive: true);
        }
    }

    [Fact]
    public async Task QuickSaveAsync_WithChangedContent_WritesFileAndClearsEditingFlag()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            vm.NotesContent = "new content";

            await vm.QuickSaveAsync();

            (await File.ReadAllTextAsync(Path.Combine(dir, ".bishop", "BISHOP_NOTES.md")))
                .Should().Be("new content");
            vm.SaveStatusIsEditing.Should().BeFalse();
            vm.SaveStatusIsError.Should().BeFalse();
            vm.SaveStatusText.Should().StartWith("Saved");
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ReloadCommand_WhenFileExists_LoadsFileContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            await File.WriteAllTextAsync(Path.Combine(dir, ".bishop", "BISHOP_NOTES.md"), "reloaded content");

            await vm.ReloadCommand.ExecuteAsync(null);

            vm.NotesContent.Should().Be("reloaded content");
            vm.SaveStatusIsEditing.Should().BeFalse();
            vm.IsExternalChangeBarVisible.Should().BeFalse();
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task FlushAsync_WithChangedContent_WritesContentToFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            vm.NotesContent = "flush content";

            await vm.FlushAsync();

            (await File.ReadAllTextAsync(Path.Combine(dir, ".bishop", "BISHOP_NOTES.md")))
                .Should().Be("flush content");
            vm.SaveStatusIsEditing.Should().BeFalse();
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ToggleCommand_WhenCollapsingWithWorkspace_WritesNotes()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            vm.IsExpanded = true;
            vm.NotesContent = "collapse notes";

            await vm.ToggleCommand.ExecuteAsync(null);

            vm.IsExpanded.Should().BeFalse();
            (await File.ReadAllTextAsync(Path.Combine(dir, ".bishop", "BISHOP_NOTES.md")))
                .Should().Be("collapse notes");
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task NotesContent_WhenChanged_EventuallyWritesToFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm();
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            vm.NotesContent = "debounced content";

            await Task.Delay(800);

            (await File.ReadAllTextAsync(Path.Combine(dir, ".bishop", "BISHOP_NOTES.md")))
                .Should().Be("debounced content");
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task QuickSaveAsync_WhenContentUnchanged_SetsSaveStatusTextToExactTime()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 3, 14, 9, 26, 53, TimeSpan.Zero));
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm(fakeTime);
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            // QuickSaveAsync with no content change hits the unchanged-content branch in WriteNotesAsync
            await vm.QuickSaveAsync();

            vm.SaveStatusText.Should().Be("Saved 09:26:53");
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task QuickSaveAsync_WithChangedContent_SetsSaveStatusTextToExactTime()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 3, 14, 15, 45, 00, TimeSpan.Zero));
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var vm = NewVm(fakeTime);
        try
        {
            await vm.LoadAsync(Guid.NewGuid(), dir);
            vm.NotesContent = "new content";

            await vm.QuickSaveAsync();

            vm.SaveStatusText.Should().Be("Saved 15:45:00");
        }
        finally
        {
            vm.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    private static WorkspaceNotesViewModel NewVm(TimeProvider? timeProvider = null) =>
        new(new FakeUiDispatcher(), timeProvider ?? new FakeTimeProvider(), new PassThroughSafeAsyncRunner());

    private sealed class FakeUiDispatcher : IUiDispatcher
    {
        public void TryEnqueue(Action work) => work();
        public void TryEnqueue(Func<Task> work) => _ = work();
    }

    private sealed class PassThroughSafeAsyncRunner : ISafeAsyncRunner
    {
        public Task RunAsync(Func<Task> action) => action();
    }
}

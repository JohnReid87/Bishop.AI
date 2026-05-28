using Bishop.ViewModels;
using FluentAssertions;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Bishop.Tests.ViewModels;

public class ScriptItemViewModelTests
{
    [Fact]
    public void Constructor_SetsNameAndPath()
    {
        var vm = new ScriptItemViewModel("my-script", @"C:\scripts\my-script.ps1");

        vm.Name.Should().Be("my-script");
        vm.Path.Should().Be(@"C:\scripts\my-script.ps1");
    }

    [Fact]
    public void Args_DefaultsToEmptyString()
    {
        var vm = new ScriptItemViewModel("x", @"C:\x.ps1");

        vm.Args.Should().Be(string.Empty);
    }

    [Fact]
    public void Args_CanBeSet()
    {
        var vm = new ScriptItemViewModel("x", @"C:\x.ps1");

        vm.Args = "-Verbose";

        vm.Args.Should().Be("-Verbose");
    }

    [Fact]
    public void Args_RaisesPropertyChanged()
    {
        var vm = new ScriptItemViewModel("x", @"C:\x.ps1");
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Args))
                raised = true;
        };

        vm.Args = "-Verbose";

        raised.Should().BeTrue();
    }

    [Fact]
    public void DeleteCommand_DeletesFileFromDisk()
    {
        var tempPath = System.IO.Path.GetTempFileName();
        var vm = new ScriptItemViewModel("s", tempPath);

        vm.DeleteCommand.Execute(null);

        System.IO.File.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public void DeleteCommand_InvokesOnDeletedCallback()
    {
        var tempPath = System.IO.Path.GetTempFileName();
        ScriptItemViewModel? callbackArg = null;
        var vm = new ScriptItemViewModel("s", tempPath, d => callbackArg = d);

        vm.DeleteCommand.Execute(null);

        callbackArg.Should().BeSameAs(vm);
    }

    [Fact]
    public void DeleteCommand_DoesNotThrow_WhenNoCallbackSupplied()
    {
        var tempPath = System.IO.Path.GetTempFileName();
        var vm = new ScriptItemViewModel("s", tempPath);

        var act = () => vm.DeleteCommand.Execute(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void EditCommand_ThrowsWin32Exception_WhenFileNotFound()
    {
        var nonExistentPath = System.IO.Path.Combine(@"C:\", Guid.NewGuid().ToString("N"), "test.txt");
        var vm = new ScriptItemViewModel("s", nonExistentPath);

        Action act = () => vm.EditCommand.Execute(null);

        act.Should().Throw<Win32Exception>();
    }

    [Fact]
    public void EditCommand_LaunchesProcess_WithCorrectPath()
    {
        ProcessStartInfo? captured = null;
        var path = @"C:\scripts\my-script.ps1";
        var vm = new ScriptItemViewModel("s", path, processLauncher: psi => captured = psi);

        vm.EditCommand.Execute(null);

        captured.Should().NotBeNull();
        captured!.FileName.Should().Be(path);
        captured.UseShellExecute.Should().BeTrue();
    }

    [Fact]
    public void DeleteCommand_Propagates_IOException()
    {
        var vm = new ScriptItemViewModel("s", @"C:\x.ps1", fileDeleter: _ => throw new IOException("locked"));

        Action act = () => vm.DeleteCommand.Execute(null);

        act.Should().Throw<IOException>();
    }

    [Fact]
    public void DeleteCommand_Propagates_UnauthorizedAccessException()
    {
        var vm = new ScriptItemViewModel("s", @"C:\x.ps1", fileDeleter: _ => throw new UnauthorizedAccessException());

        Action act = () => vm.DeleteCommand.Execute(null);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Constructor_AcceptsNullAndEmptyArguments()
    {
        var act1 = () => new ScriptItemViewModel(null!, @"C:\x.ps1");
        var act2 = () => new ScriptItemViewModel("", @"C:\x.ps1");
        var act3 = () => new ScriptItemViewModel("s", null!);
        var act4 = () => new ScriptItemViewModel("s", "");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
    }
}

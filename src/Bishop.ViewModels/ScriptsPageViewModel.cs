using Bishop.App.Scripts.GetScripts;
using Bishop.App.Scripts.LaunchScript;
using Bishop.App.Skills.LaunchSkill;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Bishop.ViewModels;

public sealed partial class ScriptsPageViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly Action<ProcessStartInfo> _processLauncher;

    public ObservableCollection<ScriptItemViewModel> Scripts { get; } = [];

    public ScriptsPageViewModel(ISender mediator, Action<ProcessStartInfo>? processLauncher = null)
    {
        _mediator = mediator;
        _processLauncher = processLauncher ?? (psi => Process.Start(psi));
    }

    [RelayCommand]
    public async Task LoadScriptsAsync()
    {
        var scripts = await _mediator.Send(new GetScriptsQuery());
        Scripts.Clear();
        foreach (var s in scripts)
            Scripts.Add(new ScriptItemViewModel(s.Name, s.Path, item => Scripts.Remove(item)));
    }

    [RelayCommand]
    public async Task RunScriptAsync(ScriptItemViewModel script)
    {
        await _mediator.Send(new LaunchScriptCommand(script.Path, script.Args));
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(ScriptsFolder);
        _processLauncher(new ProcessStartInfo("explorer.exe", ScriptsFolder) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CreateNewScriptAsync()
    {
        Directory.CreateDirectory(ScriptsFolder);
        await _mediator.Send(new LaunchSkillCommand(ScriptsFolder, "/bish-scripts"));
    }

    private static string ScriptsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bishop.AI", "scripts");
}

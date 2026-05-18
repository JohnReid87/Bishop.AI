using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.UI.ViewModels;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;

namespace Bishop.UI.Views;

public sealed partial class WorkspaceDetailPage : Page
{
    private WorkspaceItemViewModel? _item;

    public WorkspaceBoardViewModel Board { get; }

    public WorkspaceDetailPage()
    {
        Board = App.Services.GetRequiredService<WorkspaceBoardViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;

        if (e.Parameter is WorkspaceItemViewModel vm)
        {
            _item = vm;
            _item.PropertyChanged += OnItemPropertyChanged;
            UpdateView(vm);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_item is not null)
            _item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceItemViewModel.IsPathMissing))
            UpdatePathStatus();
    }

    private void UpdateView(WorkspaceItemViewModel vm)
    {
        WorkspaceNameText.Text = vm.Name;
        WorkspacePathText.Text = vm.Path;
        UpdatePathStatus();
        _ = Board.LoadAsync(vm.Id);
    }

    private void UpdatePathStatus()
    {
        var missing = _item?.IsPathMissing ?? false;
        LaunchButton.IsEnabled = !missing;
        PathWarningBar.IsOpen = missing;
        ToolTipService.SetToolTip(LaunchButtonWrapper, missing ? "The workspace directory is missing." : null);
    }

    private async void LaunchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_item is null) return;
        var mediator = App.Services.GetRequiredService<IMediator>();
        var launchedWithTerminal = await mediator.Send(new LaunchWorkspaceCommand(_item.Path));
        FallbackWarningBar.IsOpen = !launchedWithTerminal;
    }
}

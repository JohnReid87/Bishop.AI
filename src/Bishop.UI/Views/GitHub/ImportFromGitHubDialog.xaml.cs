using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.GitHub;

public sealed partial class ImportFromGitHubDialog : ContentDialog
{
    private readonly ISafeAsyncRunner _safeAsync;

    public ImportFromGitHubDialogViewModel ViewModel { get; }

    public ImportFromGitHubDialog(ImportFromGitHubDialogViewModel vm)
    {
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        ViewModel = vm;
        InitializeComponent();
        Bishop.UI.Animations.EntranceAnimation.ApplyDialogEntrance(Content as Microsoft.UI.Xaml.FrameworkElement);
        IsPrimaryButtonEnabled = false;
        Loaded += (_, _) => _safeAsync.RunAsync(ViewModel.LoadLabelsAsync);
        PrimaryButtonClick += OnImportClick;
    }

    private async void Preview_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await _safeAsync.RunAsync(async () =>
        {
            IsPrimaryButtonEnabled = false;
            var canImport = await ViewModel.PreviewAsync();
            IsPrimaryButtonEnabled = canImport;
        });

    private async void OnImportClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        => await _safeAsync.RunAsync(async () =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = true;
            IsPrimaryButtonEnabled = false;
            var success = await ViewModel.ImportAsync();
            if (!success) IsPrimaryButtonEnabled = true;
            deferral.Complete();
        });
}

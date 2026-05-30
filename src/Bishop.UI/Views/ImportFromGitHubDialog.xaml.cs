using Bishop.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class ImportFromGitHubDialog : ContentDialog
{
    public ImportFromGitHubDialogViewModel ViewModel { get; }

    public ImportFromGitHubDialog(ImportFromGitHubDialogViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
        IsPrimaryButtonEnabled = false;
        Loaded += (_, _) => SafeAsync.RunAsync(ViewModel.LoadLabelsAsync);
        PrimaryButtonClick += OnImportClick;
    }

    private async void Preview_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => await SafeAsync.RunAsync(async () =>
        {
            IsPrimaryButtonEnabled = false;
            var canImport = await ViewModel.PreviewAsync();
            IsPrimaryButtonEnabled = canImport;
        });

    private async void OnImportClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        => await SafeAsync.RunAsync(async () =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = true;
            IsPrimaryButtonEnabled = false;
            var success = await ViewModel.ImportAsync();
            if (!success) IsPrimaryButtonEnabled = true;
            deferral.Complete();
        });
}

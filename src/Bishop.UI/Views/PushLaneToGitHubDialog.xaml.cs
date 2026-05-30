using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Shared;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class PushLaneToGitHubDialog : ContentDialog
{
    public PushLaneToGitHubDialogViewModel ViewModel { get; }

    public PushLaneToGitHubDialog(PushLaneToGitHubDialogViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
        IsPrimaryButtonEnabled = ViewModel.HasWillPush;
        PrimaryButtonClick += OnPushClick;
    }

    private async void OnPushClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        => await SafeAsync.RunAsync(async () =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = true;
            ViewModel.IsBusy = true;
            IsPrimaryButtonEnabled = false;
            try
            {
                await ViewModel.PushAsync();
            }
            catch (Exception ex)
            {
                ViewModel.ApplyError(ex.Message);
                IsPrimaryButtonEnabled = true;
            }
            finally
            {
                ViewModel.IsBusy = false;
                deferral.Complete();
            }
        });
}

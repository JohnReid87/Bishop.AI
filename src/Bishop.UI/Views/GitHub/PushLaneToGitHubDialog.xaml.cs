using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views.GitHub;

public sealed partial class PushLaneToGitHubDialog : ContentDialog
{
    private readonly ISafeAsyncRunner _safeAsync;

    public PushLaneToGitHubDialogViewModel ViewModel { get; }

    public PushLaneToGitHubDialog(PushLaneToGitHubDialogViewModel vm)
    {
        _safeAsync = App.Services.GetRequiredService<ISafeAsyncRunner>();
        ViewModel = vm;
        InitializeComponent();
        IsPrimaryButtonEnabled = ViewModel.HasWillPush;
        PrimaryButtonClick += OnPushClick;
    }

    private async void OnPushClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        => await _safeAsync.RunAsync(async () =>
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

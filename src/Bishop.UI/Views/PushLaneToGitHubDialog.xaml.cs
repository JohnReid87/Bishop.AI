using Bishop.App.Cards.PushLane;
using Bishop.ViewModels;
using MediatR;
using Microsoft.UI.Xaml.Controls;

namespace Bishop.UI.Views;

public sealed partial class PushLaneToGitHubDialog : ContentDialog
{
    private readonly Guid _workspaceId;
    private readonly string _laneName;
    private readonly ISender _mediator;

    public PushLaneToGitHubDialogViewModel ViewModel { get; }

    public PushLaneToGitHubDialog(Guid workspaceId, string laneName, IReadOnlyList<CardViewModel> cards, ISender mediator)
    {
        _workspaceId = workspaceId;
        _laneName = laneName;
        _mediator = mediator;
        ViewModel = new PushLaneToGitHubDialogViewModel(cards);
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
                var result = await _mediator.Send(new PushLaneCommand(_workspaceId, _laneName));
                ViewModel.ApplyResult(result.Pushed.Count, result.SkippedAlreadyLinked, result.Failed.Count);
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

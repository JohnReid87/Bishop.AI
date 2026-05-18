using Bishop.App.Cards.RemoveCard;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using Microsoft.UI.Xaml;

namespace Bishop.UI.ViewModels;

public sealed partial class CardDetailDialogViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public Guid CardId { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<string> Tags { get; }

    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NoDescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TagsVisibility =>
        Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteConfirmVisibility), nameof(DeleteButtonVisibility))]
    public partial bool ShowDeleteConfirm { get; set; }

    [ObservableProperty]
    public partial bool Deleted { get; set; }

    public Visibility DeleteConfirmVisibility =>
        ShowDeleteConfirm ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DeleteButtonVisibility =>
        ShowDeleteConfirm ? Visibility.Collapsed : Visibility.Visible;

    public CardDetailDialogViewModel(CardViewModel card, IMediator mediator)
    {
        _mediator = mediator;
        CardId = card.Id;
        Title = card.Title;
        Description = card.Description;
        Tags = card.Tags;
    }

    [RelayCommand]
    private void RequestDelete() => ShowDeleteConfirm = true;

    [RelayCommand]
    private void CancelDelete() => ShowDeleteConfirm = false;

    [RelayCommand]
    private async Task ConfirmDeleteAsync(CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveCardCommand(CardId), cancellationToken);
        Deleted = true;
    }
}

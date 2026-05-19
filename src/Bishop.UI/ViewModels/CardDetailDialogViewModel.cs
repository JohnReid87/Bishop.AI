using Bishop.App.Cards.RemoveCard;
using Bishop.Core.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using Microsoft.UI.Xaml;

namespace Bishop.UI.ViewModels;

public sealed partial class CardDetailDialogViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public Guid CardId { get; }
    public int Number { get; }
    public string Title { get; }
    public string Description { get; }
    public string LaneName { get; }
    public IReadOnlyList<CardTagViewModel> Tags { get; }
    public Visibility SkillsButtonVisibility { get; }

    public string NumberDisplay => $"#{Number}";

    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NoDescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TagsVisibility =>
        Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteConfirmVisibility))]
    public partial bool ShowDeleteConfirm { get; set; }

    [ObservableProperty]
    public partial bool Deleted { get; set; }

    public Visibility DeleteConfirmVisibility =>
        ShowDeleteConfirm ? Visibility.Visible : Visibility.Collapsed;

    public CardDetailDialogViewModel(CardViewModel card, IReadOnlyList<InstalledSkill> cardSkills, IMediator mediator)
    {
        _mediator = mediator;
        CardId = card.Id;
        Number = card.Number;
        Title = card.Title;
        Description = card.Description;
        LaneName = card.LaneName;
        Tags = card.Tags;
        SkillsButtonVisibility = cardSkills.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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

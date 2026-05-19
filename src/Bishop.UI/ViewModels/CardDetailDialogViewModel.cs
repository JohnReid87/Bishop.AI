using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.UpdateCard;
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
    public string LaneName { get; }
    public IReadOnlyList<CardTagViewModel> Tags { get; }
    public Visibility SkillsButtonVisibility { get; }
    public bool Updated { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleViewVisibility), nameof(TitleEditVisibility))]
    public partial bool IsTitleEditing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionViewVisibility), nameof(DescriptionEditVisibility))]
    public partial bool IsDescriptionEditing { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionVisibility), nameof(NoDescriptionVisibility))]
    public partial string Description { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditErrorVisibility))]
    public partial string? EditError { get; set; }

    public string NumberDisplay => $"#{Number}";

    public Visibility TitleViewVisibility => IsTitleEditing ? Visibility.Collapsed : Visibility.Visible;
    public Visibility TitleEditVisibility => IsTitleEditing ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DescriptionViewVisibility => IsDescriptionEditing ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DescriptionEditVisibility => IsDescriptionEditing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NoDescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TagsVisibility =>
        Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EditErrorVisibility =>
        string.IsNullOrEmpty(EditError) ? Visibility.Collapsed : Visibility.Visible;

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

    public void StartTitleEdit() => IsTitleEditing = true;

    public void CancelTitleEdit() => IsTitleEditing = false;

    public async Task CommitTitleAsync(string draft)
    {
        var trimmed = draft.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == Title)
        {
            IsTitleEditing = false;
            return;
        }

        var prior = Title;
        Title = trimmed;
        IsTitleEditing = false;

        try
        {
            await _mediator.Send(new UpdateCardCommand(CardId, trimmed, null, false, []));
            EditError = null;
            Updated = true;
        }
        catch
        {
            Title = prior;
            EditError = "Failed to save title.";
        }
    }

    public void StartDescriptionEdit() => IsDescriptionEditing = true;

    public void CancelDescriptionEdit() => IsDescriptionEditing = false;

    public async Task CommitDescriptionAsync(string draft)
    {
        var trimmed = draft.TrimEnd();
        if (trimmed == Description)
        {
            IsDescriptionEditing = false;
            return;
        }

        var prior = Description;
        Description = trimmed;
        IsDescriptionEditing = false;

        try
        {
            await _mediator.Send(new UpdateCardCommand(CardId, null, trimmed, false, []));
            EditError = null;
            Updated = true;
        }
        catch
        {
            Description = prior;
            EditError = "Failed to save description.";
        }
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

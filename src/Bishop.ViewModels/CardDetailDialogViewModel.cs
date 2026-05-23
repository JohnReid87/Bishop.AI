using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Claude;
using Bishop.App.Git;
using Bishop.App.Skills;
using Bishop.App.Tags.ListTagsByWorkspace;
using Bishop.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;

namespace Bishop.ViewModels;

public sealed partial class CardDetailDialogViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly Guid _workspaceId;
    private readonly string? _workspaceGitHubRepo;

    public Guid CardId { get; }
    public int Number { get; }
    public string LaneName { get; }
    public bool IsSkillsButtonVisible { get; }
    public bool IsPushSectionVisible => _workspaceGitHubRepo is not null;
    public bool Updated { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTagVisible), nameof(IsAddTagButtonVisible))]
    public partial string? TagName { get; set; }

    [ObservableProperty]
    public partial string? TagColour { get; set; }

    public bool IsTagVisible => TagName is not null;
    public bool IsAddTagButtonVisible => TagName is null;

    [ObservableProperty]
    public partial bool IsTitleEditing { get; set; }

    [ObservableProperty]
    public partial bool IsDescriptionEditing { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    public partial string Description { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditError))]
    public partial string? EditError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloseReopenText))]
    public partial bool IsClosed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPushToGitHub), nameof(IsPushButtonVisible), nameof(IsGitHubLinkVisible), nameof(GitHubIssueUrl))]
    public partial int? GitHubIssueNumber { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPushError))]
    public partial string? PushError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaudeTotals))]
    public partial string? ClaudeTotalsText { get; set; }

    public bool HasClaudeTotals => !string.IsNullOrEmpty(ClaudeTotalsText);

    public void SetClaudeTotals(int inputTokens, int outputTokens, int runCount) =>
        ClaudeTotalsText = ClaudeTotalsFormatter.Format(inputTokens, outputTokens, runCount);

    public string NumberDisplay => $"#{Number}";

    public string CloseReopenText => IsClosed ? "Reopen" : "Close";

    public bool CanPushToGitHub => GitHubIssueNumber is null && _workspaceGitHubRepo is not null;

    public string? GitHubIssueUrl => GitHubIssueNumber is not null && _workspaceGitHubRepo is not null
        ? $"https://github.com/{_workspaceGitHubRepo}/issues/{GitHubIssueNumber}"
        : null;

    public bool IsPushButtonVisible => GitHubIssueNumber is null;
    public bool IsGitHubLinkVisible => GitHubIssueNumber is not null;
    public bool HasPushError => !string.IsNullOrEmpty(PushError);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommitVisible), nameof(IsCommitTextVisible))]
    public partial string? CommitShortHash { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommitLinkVisible), nameof(IsCommitTextVisible), nameof(CommitUrl))]
    public partial string? CommitHash { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommitLinkVisible), nameof(IsCommitTextVisible), nameof(CommitUrl))]
    public partial bool CommitIsPushed { get; set; }

    public string? CommitUrl => CommitIsPushed && CommitHash is not null && _workspaceGitHubRepo is not null
        ? $"https://github.com/{_workspaceGitHubRepo}/commit/{CommitHash}"
        : null;

    public bool IsCommitVisible => !string.IsNullOrEmpty(CommitShortHash);
    public bool IsCommitLinkVisible => CommitUrl is not null;
    public bool IsCommitTextVisible => !string.IsNullOrEmpty(CommitShortHash) && CommitUrl is null;

    public void SetCommit(CommitInfo commit)
    {
        CommitHash = commit.FullHash;
        CommitIsPushed = commit.IsPushed;
        CommitShortHash = commit.ShortHash;
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasEditError => !string.IsNullOrEmpty(EditError);

    [ObservableProperty]
    public partial bool ShowDeleteConfirm { get; set; }

    [ObservableProperty]
    public partial bool Deleted { get; set; }

    public CardDetailDialogViewModel(CardViewModel card, SkillMenuItem[] cardSkills, Guid workspaceId, string? gitHubRepo, IMediator mediator)
    {
        _mediator = mediator;
        _workspaceId = workspaceId;
        _workspaceGitHubRepo = gitHubRepo;
        CardId = card.Id;
        Number = card.Number;
        Title = card.Title;
        Description = card.Description;
        LaneName = card.LaneName;
        IsClosed = card.IsClosed;
        GitHubIssueNumber = card.GitHubIssueNumber;
        TagName = card.TagName;
        TagColour = card.TagColour;
        IsSkillsButtonVisible = cardSkills.Length > 0;
    }

    public Task<IReadOnlyList<TagInfo>> GetWorkspaceTagsAsync() =>
        _mediator.Send(new ListTagsByWorkspaceQuery(_workspaceId));

    public async Task ClearTagAsync()
    {
        var priorName = TagName;
        var priorColour = TagColour;
        TagName = null;
        TagColour = null;
        try
        {
            await _mediator.Send(new UpdateCardCommand(CardId, null, null, true, null));
            EditError = null;
            Updated = true;
        }
        catch
        {
            TagName = priorName;
            TagColour = priorColour;
            EditError = "Failed to remove tag.";
        }
    }

    public async Task SetTagAsync(string name, string colour)
    {
        var priorName = TagName;
        var priorColour = TagColour;
        TagName = name;
        TagColour = colour;
        try
        {
            await _mediator.Send(new UpdateCardCommand(CardId, null, null, true, name));
            EditError = null;
            Updated = true;
        }
        catch
        {
            TagName = priorName;
            TagColour = priorColour;
            EditError = "Failed to add tag.";
        }
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
            await _mediator.Send(new UpdateCardCommand(CardId, trimmed, null, false, null));
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
            await _mediator.Send(new UpdateCardCommand(CardId, null, trimmed, false, null));
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
    private async Task ToggleClosedAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsClosed)
                await _mediator.Send(new ReopenCardCommand(CardId), cancellationToken);
            else
                await _mediator.Send(new CloseCardCommand(CardId), cancellationToken);

            IsClosed = !IsClosed;
            Updated = true;
        }
        catch
        {
            EditError = "Failed to update closed state.";
        }
    }

    [RelayCommand]
    private async Task PushToGitHubAsync(CancellationToken cancellationToken)
    {
        PushError = null;
        try
        {
            var card = await _mediator.Send(new PushCardCommand(CardId), cancellationToken);
            GitHubIssueNumber = card.GitHubIssueNumber;
            Updated = true;
        }
        catch (Exception ex)
        {
            PushError = ex.Message;
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

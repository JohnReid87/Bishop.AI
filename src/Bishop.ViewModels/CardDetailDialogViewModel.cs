using System.Text.RegularExpressions;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Services.Claude;
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
    private static readonly Regex CardRefRegex = new(
        @"(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`]*`)|(\\#\d+)|((?<!\w)#(\d+)\b)",
        RegexOptions.Compiled);

    private readonly ISender _mediator;
    private readonly Guid _workspaceId;
    private readonly string? _workspaceGitHubRepo;
    private HashSet<int>? _validCardNumbers;
    private Guid _cardId;

    public Guid CardId => _cardId;
    public bool IsSkillsButtonVisible { get; }
    public bool IsPushSectionVisible => _workspaceGitHubRepo is not null;
    public bool Updated { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NumberDisplay))]
    public partial int Number { get; set; }

    [ObservableProperty]
    public partial string LaneName { get; set; }

    [ObservableProperty]
    public partial bool CanGoBack { get; set; }

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
    [NotifyPropertyChangedFor(nameof(HasDescription), nameof(LinkableDescription))]
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

    public string LinkableDescription =>
        _validCardNumbers is null
            ? Description
            : CardRefRegex.Replace(Description ?? string.Empty, match =>
            {
                if (match.Groups[1].Success || match.Groups[2].Success)
                    return match.Value;
                var number = int.Parse(match.Groups[4].Value);
                return _validCardNumbers.Contains(number)
                    ? $"[#{number}](bishop://card/{number})"
                    : $"~~#{number}~~";
            });

    [ObservableProperty]
    public partial bool ShowDeleteConfirm { get; set; }

    [ObservableProperty]
    public partial bool Deleted { get; set; }

    public CardDetailDialogViewModel(CardViewModel card, SkillMenuItem[] cardSkills, Guid workspaceId, string? gitHubRepo, ISender mediator)
    {
        _mediator = mediator;
        _workspaceId = workspaceId;
        _workspaceGitHubRepo = gitHubRepo;
        _cardId = card.Id;
        Number = card.Number;
        LaneName = card.LaneName;
        Title = card.Title;
        Description = card.Description;
        IsClosed = card.IsClosed;
        GitHubIssueNumber = card.GitHubIssueNumber;
        TagName = card.TagName;
        TagColour = card.TagColour;
        IsSkillsButtonVisible = cardSkills.Length > 0;
    }

    public async Task LoadCardNumbersAsync()
    {
        try
        {
            var cards = await _mediator.Send(new ListCardsByWorkspaceQuery(_workspaceId));
            _validCardNumbers = cards.Select(c => c.Number).ToHashSet();
            OnPropertyChanged(nameof(LinkableDescription));
        }
        catch
        {
            // Description stays unlinked if load fails.
        }
    }

    public void NavigateTo(CardViewModel card, bool canGoBack)
    {
        _cardId = card.Id;
        Number = card.Number;
        LaneName = card.LaneName;
        Title = card.Title;
        Description = card.Description;
        IsClosed = card.IsClosed;
        GitHubIssueNumber = card.GitHubIssueNumber;
        TagName = card.TagName;
        TagColour = card.TagColour;
        IsTitleEditing = false;
        IsDescriptionEditing = false;
        ShowDeleteConfirm = false;
        EditError = null;
        PushError = null;
        CommitShortHash = null;
        CommitHash = null;
        CommitIsPushed = false;
        ClaudeTotalsText = null;
        CanGoBack = canGoBack;
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

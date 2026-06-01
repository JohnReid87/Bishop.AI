using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Services.Claude;
using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Tags.ListTags;
using Bishop.Core;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Skills;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bishop.ViewModels.Cards;

public sealed partial class CardDetailDialogViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;
    private readonly ILogger<CardDetailDialogViewModel> _logger;
    private readonly IErrorBus _errorBus;
    private readonly Guid _workspaceId;
    private readonly string? _workspaceGitHubRepo;
    private readonly string _workspacePath;
    private readonly CardLinkRenderer _linkRenderer = new();
    private readonly CardExtrasLoader _extrasLoader;
    private Guid _cardId;

    public Guid CardId => _cardId;
    public bool IsSkillsButtonVisible { get; }
    public bool IsPushSectionVisible => _workspaceGitHubRepo is not null;
    public SkillMenuItem[] CardSkills { get; private set; } = [];
    public string WorkspacePath => _workspacePath;
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
    [NotifyPropertyChangedFor(nameof(IsPushButtonVisible), nameof(IsGitHubLinkVisible))]
    public partial int? GitHubIssueNumber { get; set; }

    [ObservableProperty]
    public partial string? GitHubIssueUrl { get; private set; }

    [ObservableProperty]
    public partial bool CanPushToGitHub { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPushError))]
    public partial string? PushError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaudeTotals))]
    public partial string? ClaudeTotalsText { get; set; }

    public bool HasClaudeTotals => !string.IsNullOrEmpty(ClaudeTotalsText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailedRunTranscript))]
    public partial string? LastFailedRunTranscriptPath { get; set; }

    public bool HasFailedRunTranscript => !string.IsNullOrEmpty(LastFailedRunTranscriptPath);

    public void SetClaudeTotals(int inputTokens, int outputTokens, int runCount) =>
        ClaudeTotalsText = ClaudeTotalsFormatter.Format(inputTokens, outputTokens, runCount);

    public string NumberDisplay => $"#{Number}";

    public string CloseReopenText => IsClosed ? "Reopen" : "Close";

    public bool IsPushButtonVisible => GitHubIssueNumber is null;
    public bool IsGitHubLinkVisible => GitHubIssueNumber is not null;
    public bool HasPushError => !string.IsNullOrEmpty(PushError);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCommitVisible))]
    public partial string? CommitShortHash { get; private set; }

    [ObservableProperty]
    public partial string? CommitUrl { get; private set; }

    [ObservableProperty]
    public partial bool IsCommitLinkVisible { get; private set; }

    [ObservableProperty]
    public partial bool IsCommitTextVisible { get; private set; }

    public bool IsCommitVisible => !string.IsNullOrEmpty(CommitShortHash);

    public void SetCommit(CommitInfo commit)
    {
        var state = CardCommitState.From(commit, _workspaceGitHubRepo);
        CommitShortHash = state.ShortHash;
        CommitUrl = state.Url;
        IsCommitLinkVisible = state.IsLinkVisible;
        IsCommitTextVisible = state.IsTextVisible;
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasEditError => !string.IsNullOrEmpty(EditError);

    public string LinkableDescription => _linkRenderer.Render(Description ?? string.Empty);

    [ObservableProperty]
    public partial bool ShowDeleteConfirm { get; set; }

    [ObservableProperty]
    public partial bool Deleted { get; set; }

    public CardDetailDialogViewModel(CardViewModel card, SkillMenuItem[] cardSkills, Guid workspaceId, string? gitHubRepo, ISender mediator, IAppSettings appSettings, string workspacePath, ILogger<CardDetailDialogViewModel> logger, IErrorBus errorBus)
    {
        _mediator = mediator;
        _appSettings = appSettings;
        _logger = logger;
        _errorBus = errorBus;
        _workspaceId = workspaceId;
        _workspaceGitHubRepo = gitHubRepo;
        _workspacePath = workspacePath;
        _extrasLoader = new CardExtrasLoader(mediator, logger, errorBus);
        _cardId = card.Id;
        Number = card.Number;
        LaneName = card.LaneName;
        Title = card.Title;
        Description = card.Description;
        IsClosed = card.IsClosed;
        GitHubIssueNumber = card.GitHubIssueNumber;
        OnGitHubIssueNumberChanged(card.GitHubIssueNumber);
        TagName = card.TagName;
        TagColour = card.TagColour;
        CardSkills = cardSkills;
        IsSkillsButtonVisible = cardSkills.Length > 0;
    }

    partial void OnGitHubIssueNumberChanged(int? value)
    {
        var state = CardGitHubUrlState.For(value, _workspaceGitHubRepo);
        GitHubIssueUrl = state.IssueUrl;
        CanPushToGitHub = state.CanPush;
    }

    public async Task LoadCardNumbersAsync()
    {
        await _linkRenderer.LoadAsync(_mediator, _workspaceId, _logger, _cardId);
        OnPropertyChanged(nameof(LinkableDescription));
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
        CommitUrl = null;
        IsCommitLinkVisible = false;
        IsCommitTextVisible = false;
        ClaudeTotalsText = null;
        LastFailedRunTranscriptPath = null;
        CanGoBack = canGoBack;
    }

    public async Task<IReadOnlyList<TagInfo>> GetWorkspaceTagsAsync()
    {
        try
        {
            return await _mediator.Send(new ListTagsQuery());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace tags for {WorkspaceId}; returning empty list", _workspaceId);
            return [];
        }
    }

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear tag on card {CardId}", CardId);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set tag on card {CardId}", CardId);
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
        catch (Exception ex)
        {
            Title = prior;
            EditError = ex.Message;
        }
    }

    public void StartDescriptionEdit() => IsDescriptionEditing = true;

    public void CancelDescriptionEdit() => IsDescriptionEditing = false;

    public async Task CommitDescriptionAsync(string draft)
    {
        var trimmed = (draft ?? string.Empty).TrimEnd();
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
        catch (Exception ex)
        {
            Description = prior;
            EditError = ex.Message;
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle closed state on card {CardId}", CardId);
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

    // ── Card extras (commit + Claude totals) ─────────────────────────────────

    public async Task LoadExtrasAsync()
    {
        var result = await _extrasLoader.LoadAsync(_cardId, _workspacePath, Number);
        if (result is null) return;
        SetClaudeTotals(result.InputTokens, result.OutputTokens, result.RunCount);
        LastFailedRunTranscriptPath = result.FailedTranscriptPath;
        if (result.Commit is { } commit) SetCommit(commit);
    }

    public async Task<CardViewModel?> GetCardByNumberAsync(int number, bool isSkillsButtonVisible)
    {
        var card = await _mediator.Send(new GetCardByNumberQuery(number, _workspaceId));
        if (card is null) return null;

        var tags = await GetWorkspaceTagsAsync();
        var tagColour = tags.FirstOrDefault(t => t.Name == card.TagName)?.Colour;

        return new CardViewModel
        {
            Id = card.Id,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = card.LaneName,
            TagName = card.TagName,
            TagColour = tagColour,
            IsClosed = card.IsClosed,
            GitHubIssueNumber = card.GitHubIssueNumber,
            GitHubPushedAt = card.GitHubPushedAt,
            LastAutoRunFailedAt = card.LastAutoRunFailedAt,
            LastAutoRunSucceededAt = card.LastAutoRunSucceededAt,
            IsSkillsButtonVisible = isSkillsButtonVisible,
        };
    }

    // ── Skill launch + model persistence ─────────────────────────────────────

    public async Task<IReadOnlyList<SkillLaunchItem>> BuildSkillLaunchItemsAsync()
    {
        var items = new List<SkillLaunchItem>(CardSkills.Length);
        foreach (var menuItem in CardSkills)
            items.Add(await SkillLaunchItemBuilder.BuildAsync(menuItem, Number, Title, Description, _workspacePath, _appSettings));
        return items;
    }

    public async Task LaunchAsync(SkillLaunchItem item, string? stagedText, TerminalSnap snap, string modelId)
    {
        var command = string.IsNullOrWhiteSpace(stagedText)
            ? item.RenderedCommand
            : $"{item.RenderedCommand} {stagedText}";
        await _mediator.Send(new LaunchSkillCommand(_workspacePath, command, snap, modelId));
    }

    public async Task SetSkillModelAsync(string skillName, string modelId)
        => await _appSettings.SetAsync(SkillLaunchItemBuilder.LastModelKey(skillName), modelId);
}

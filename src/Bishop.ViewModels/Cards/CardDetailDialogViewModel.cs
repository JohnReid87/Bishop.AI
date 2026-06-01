using System.IO;
using System.Text.RegularExpressions;
using Bishop.App.Cards.CloseCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Cards.GetCardByNumber;
using Bishop.App.Cards.ListCardsByWorkspace;
using Bishop.App.Cards.PushCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Cards.ReopenCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Git;
using Bishop.App.Git.GetCardCommit;
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
    private static readonly Regex CardRefRegex = new(
        @"(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`]*`)|(\\#\d+)|((?<!\w)#(\d+)\b)",
        RegexOptions.Compiled);

    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;
    private readonly ILogger<CardDetailDialogViewModel> _logger;
    private readonly IErrorBus _errorBus;
    private readonly Guid _workspaceId;
    private readonly string? _workspaceGitHubRepo;
    private readonly string _workspacePath;
    private HashSet<int>? _validCardNumbers;
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
    [NotifyPropertyChangedFor(nameof(CanPushToGitHub), nameof(IsPushButtonVisible), nameof(IsGitHubLinkVisible), nameof(GitHubIssueUrl))]
    public partial int? GitHubIssueNumber { get; set; }

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

    public CardDetailDialogViewModel(CardViewModel card, SkillMenuItem[] cardSkills, Guid workspaceId, string? gitHubRepo, ISender mediator, IAppSettings appSettings, string workspacePath, ILogger<CardDetailDialogViewModel> logger, IErrorBus errorBus)
    {
        _mediator = mediator;
        _appSettings = appSettings;
        _logger = logger;
        _errorBus = errorBus;
        _workspaceId = workspaceId;
        _workspaceGitHubRepo = gitHubRepo;
        _workspacePath = workspacePath;
        _cardId = card.Id;
        Number = card.Number;
        LaneName = card.LaneName;
        Title = card.Title;
        Description = card.Description;
        IsClosed = card.IsClosed;
        GitHubIssueNumber = card.GitHubIssueNumber;
        TagName = card.TagName;
        TagColour = card.TagColour;
        CardSkills = cardSkills;
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
        catch (Exception ex)
        {
            // intentional: description stays unlinked when card-number list unavailable
            _logger.LogDebug(ex, "Card numbers unavailable; description links disabled for card {CardId}", _cardId);
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
        try
        {
            var card = await _mediator.Send(new GetCardQuery(_cardId));
            if (card is null) return;
            SetClaudeTotals(card.TotalInputTokens, card.TotalOutputTokens, card.ClaudeRunCount);
            LastFailedRunTranscriptPath = card.LastAutoRunFailedAt.HasValue
                ? FindLatestTranscript(_workspacePath, Number)
                : null;
            var commitResult = await _mediator.Send(new GetCardCommitQuery(Number, _workspacePath));
            if (commitResult is GetCardCommitResult.Found found)
                SetCommit(found.Commit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load card extras for card {CardId}", _cardId);
            _errorBus.Report(ex);
        }
    }

    private static string? FindLatestTranscript(string workspacePath, int cardNumber)
    {
        var dir = Path.Combine(workspacePath, ".bishop", "runs");
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir, $"{cardNumber}-*.jsonl")
            .OrderDescending()
            .FirstOrDefault();
    }

    public async Task<CardViewModel?> GetCardByNumberAsync(int number, bool isSkillsButtonVisible)
    {
        var card = await _mediator.Send(new GetCardByNumberQuery(number, _workspaceId));
        if (card is null) return null;

        var tags = await GetWorkspaceTagsAsync();
        var tagColour = card.TagName is { } tagName
            ? tags.FirstOrDefault(t => t.Name == tagName)?.Colour
            : null;

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
        {
            var skill = menuItem.Skill;
            var rendered = SkillCommandRenderer.Render(skill.Command!, Number, Title, Description, _workspacePath);
            var savedModel = SkillModelOptions.ResolveModelId(
                await _appSettings.GetAsync($"skill.{skill.Name}.last_model"));

            items.Add(new SkillLaunchItem(
                Name: menuItem.Name,
                GroupHeader: menuItem.GroupHeader,
                SavedModelId: savedModel,
                RenderedCommand: rendered,
                RequiresStage: SkillStaging.ShouldShowStageDialog(skill, hasCard: true),
                StagePrompt: skill.StagePrompt,
                StagePrefill: null,
                MarkdownBody: skill.MarkdownBody));
        }
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
        => await _appSettings.SetAsync($"skill.{skillName}.last_model", modelId);
}

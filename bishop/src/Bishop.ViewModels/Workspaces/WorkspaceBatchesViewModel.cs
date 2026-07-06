using Bishop.App.Batches.AbandonBatch;
using Bishop.App.Batches.CleanUpBatch;
using Bishop.App.Batches.CreateBatch;
using Bishop.App.Batches.LaunchBatchTerminal;
using Bishop.App.Batches.ListBatches;
using Bishop.App.Batches.MergeBatch;
using Bishop.App.Batches.RemoveBatch;
using Bishop.App.Batches.RemoveCardFromBatch;
using Bishop.App.Batches.RenameBatch;
using Bishop.App.Batches.RequestStopBatch;
using Bishop.App.Batches.RescueBatch;
using Bishop.App.Batches.SalvageBatch;
using Bishop.App.Cards.MoveCard;
using Bishop.App.Services.Settings;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Skills.LaunchSkill;
using Bishop.App.Tags.ListTags;
using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;
using System.IO;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class WorkspaceBatchesViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private readonly IAppSettings _appSettings;
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;

    public ObservableCollection<BatchItemViewModel> Batches { get; } = [];

    [ObservableProperty]
    private bool _hasBatches;

    [ObservableProperty]
    private int _badgeCount;

    [ObservableProperty]
    private string _badgeColor = string.Empty;

    [ObservableProperty]
    private bool _badgeIsVisible;

    [ObservableProperty]
    private string _badgeTooltip = string.Empty;

    public WorkspaceBatchesViewModel(ISender mediator, IAppSettings appSettings)
    {
        _mediator = mediator;
        _appSettings = appSettings;
    }

    public async Task LoadAsync(Guid workspaceId, string workspacePath)
    {
        _workspaceId = workspaceId;
        _workspacePath = workspacePath;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var showClosed = await ReadShowClosedAsync();
        var summaries = await _mediator.Send(new ListBatchesQuery(_workspaceId, _workspacePath, IncludeClosed: showClosed));
        var tags = await _mediator.Send(new ListTagsQuery());
        var tagColourByName = tags.ToDictionary(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);

        Batches.Clear();
        foreach (var s in summaries)
        {
            var batchVm = new BatchItemViewModel
            {
                Id = s.Batch.Id,
                Name = s.Batch.Name,
                BranchName = s.Batch.BranchName,
                Model = s.Batch.Model,
                Status = s.Batch.Status,
                CardCount = s.CardCount,
                FinishedAt = s.FinishedAt,
                MergedAt = s.MergedAt,
                StoppedAt = s.Batch.StoppedAt,
                IsMerged = s.IsMerged,
                BranchExists = s.BranchExists,
                WorktreeExists = s.WorktreeExists,
            };

            var inProgressCardId = s.Batch.Status == BatchStatus.Working
                ? s.Cards
                    .Where(c => c.LaneName == SystemLaneNames.Doing && c.LastAutoRunFailedAt is null)
                    .MinBy(c => c.Number)?.Id
                : null;

            foreach (var card in s.Cards)
            {
                var tagColour = card.TagName is { } name && tagColourByName.TryGetValue(name, out var col) ? col : null;
                batchVm.Cards.Add(new CardViewModel
                {
                    Id = card.Id,
                    Number = card.Number,
                    Title = card.Title,
                    Description = card.Description,
                    LaneName = card.LaneName,
                    TagName = card.TagName,
                    TagColour = tagColour,
                    IsClosed = card.IsClosed,
                    LastAutoRunFailedAt = card.LastAutoRunFailedAt,
                    LastAutoRunSucceededAt = card.LastAutoRunSucceededAt,
                    BatchId = card.BatchId,
                    BatchName = s.Batch.Name,
                    BatchCreatedAt = s.Batch.CreatedAt,
                    IsInProgress = card.Id == inProgressCardId,
                    IsSkillsButtonVisible = false,
                });
            }

            Batches.Add(batchVm);
        }
        HasBatches = Batches.Count > 0;
        UpdateBadge();
    }

    private async Task<bool> ReadShowClosedAsync()
    {
        var raw = await _appSettings.GetAsync(AppSettingsKeys.ShowClosedBatches);
        return bool.TryParse(raw, out var value) && value;
    }

    private void UpdateBadge()
    {
        var readyCount = Batches.Count(b => b.FinishedAt != null);
        BadgeCount = readyCount;
        BadgeColor = readyCount > 0 ? "#c4a85f" : string.Empty;
        BadgeIsVisible = readyCount > 0;
        BadgeTooltip = readyCount > 0
            ? $"{readyCount} of {Batches.Count} batches ready to complete"
            : string.Empty;
    }

    // ── Batch operations ─────────────────────────────────────────────────────

    public async Task RequestStopAsync(Guid batchId)
        => await _mediator.Send(new RequestStopBatchCommand(batchId));

    public async Task<BatchMergeOutcome> MergeAsync(string batchName, string workspacePath)
    {
        var result = await _mediator.Send(new MergeBatchCommand(batchName, workspacePath));
        return new BatchMergeOutcome(result.Success, result.ConflictFiles, result.ErrorMessage);
    }

    public async Task CleanUpAsync(string batchName, string workspacePath)
        => await _mediator.Send(new CleanUpBatchCommand(batchName, workspacePath));

    public async Task<BatchRescueResult> RescueAsync(string batchName, bool confirmReset)
    {
        var r = await _mediator.Send(new RescueBatchCommand(batchName, confirmReset));
        var outcome = r.Outcome switch
        {
            RescueBatchOutcome.Rescued => BatchRescueOutcome.Rescued,
            RescueBatchOutcome.LockAlive => BatchRescueOutcome.LockAlive,
            RescueBatchOutcome.NotRunning => BatchRescueOutcome.NotRunning,
            RescueBatchOutcome.NeedsConfirmation => BatchRescueOutcome.NeedsConfirmation,
            _ => throw new ArgumentOutOfRangeException(nameof(r.Outcome), r.Outcome, "Unhandled rescue outcome."),
        };
        return new BatchRescueResult(outcome, r.LockOwnerPid, r.DirtyPaths, r.RequeuedCardNumbers);
    }

    public async Task<BatchSalvageResult> SalvageAsync(string batchName, string workspacePath, bool confirm)
    {
        var r = await _mediator.Send(new SalvageBatchCommand(batchName, workspacePath, confirm));
        var outcome = r.Outcome switch
        {
            SalvageBatchOutcome.NeedsConfirmation => BatchSalvageOutcome.NeedsConfirmation,
            SalvageBatchOutcome.LockAlive => BatchSalvageOutcome.LockAlive,
            SalvageBatchOutcome.NothingSucceeded => BatchSalvageOutcome.NothingSucceeded,
            SalvageBatchOutcome.MergeConflict => BatchSalvageOutcome.MergeConflict,
            SalvageBatchOutcome.Salvaged => BatchSalvageOutcome.Salvaged,
            _ => throw new ArgumentOutOfRangeException(nameof(r.Outcome), r.Outcome, "Unhandled salvage outcome."),
        };
        return new BatchSalvageResult(
            outcome,
            r.LockOwnerPid,
            r.MergedCardNumbers,
            r.EjectedCardNumbers,
            r.ClosedCardNumbers,
            r.ConflictFiles,
            r.ErrorMessage);
    }

    public async Task ReviewBatch(string workspacePath, string batchName, string model, Guid batchId, TerminalSnap snap)
        => await _mediator.Send(new LaunchSkillCommand(
            workspacePath, $"/bish-review-batch {batchName}", snap, model, batchId));

    public async Task AbandonAsync(string batchName, string workspacePath)
        => await _mediator.Send(new AbandonBatchCommand(batchName, workspacePath));

    public async Task RemoveAsync(string batchName)
        => await _mediator.Send(new RemoveBatchCommand(batchName));

    public async Task<string> RenameAsync(string oldName, string newName)
    {
        var renamed = await _mediator.Send(new RenameBatchCommand(oldName, newName));
        return renamed.Name;
    }

    public async Task CommitBatchNameAsync(BatchItemViewModel batch, string proposedName)
    {
        var trimmed = proposedName.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == batch.Name)
        {
            batch.IsNameEditing = false;
            return;
        }
        batch.Name = await RenameAsync(batch.Name, trimmed);
        batch.IsNameEditing = false;
    }

    public async Task CreateAsync(Guid workspaceId, string workspacePath, string name,
        string branchName, string worktreePath, int[] cardNumbers, string model = SkillModelOptions.DefaultModelId)
        => await _mediator.Send(new CreateBatchCommand(
            workspaceId, workspacePath, name, branchName, null, worktreePath, cardNumbers, null, null, model));

    public async Task<bool> CreateFromTrayAsync(
        Guid workspaceId,
        string workspacePath,
        string trayName,
        string? trayBranch,
        string trayModel,
        int[] cardNumbers)
    {
        if (cardNumbers.Length == 0) return false;
        var name = trayName.Trim();
        if (string.IsNullOrEmpty(name)) return false;

        var slug = BatchStagingTrayViewModel.Slugify(name);
        var branchName = string.IsNullOrWhiteSpace(trayBranch) ? $"bishop/{slug}" : trayBranch.Trim();
        var normalizedPath = workspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var repoName = Path.GetFileName(normalizedPath);
        var parentDir = Path.GetDirectoryName(normalizedPath)!;
        var worktreePath = Path.Combine(parentDir, $"{repoName}-bishop-worktrees", slug);

        await CreateAsync(workspaceId, workspacePath, name, branchName, worktreePath, cardNumbers, trayModel);
        return true;
    }

    public async Task LaunchBatch(string workspacePath, string batchName, string model, TerminalSnap snap)
        => await _mediator.Send(new LaunchBatchTerminalCommand(workspacePath, batchName, model, Resume: false, snap));

    public async Task ResumeBatch(string workspacePath, string batchName, string model, TerminalSnap snap)
        => await _mediator.Send(new LaunchBatchTerminalCommand(workspacePath, batchName, model, Resume: true, snap));

    public async Task MarkCardDoneAndResumeAsync(Guid cardId, string batchName, string workspacePath, string model, TerminalSnap snap)
    {
        await _mediator.Send(new MoveCardCommand(cardId, SystemLaneNames.Done, 1));
        await _mediator.Send(new LaunchBatchTerminalCommand(workspacePath, batchName, model, Resume: true, snap));
    }

    public async Task RemoveCardAndResumeAsync(string batchName, Guid cardId, string workspacePath, string model, TerminalSnap snap)
    {
        await _mediator.Send(new RemoveCardFromBatchCommand(batchName, cardId));
        await _mediator.Send(new LaunchBatchTerminalCommand(workspacePath, batchName, model, Resume: true, snap));
    }
}

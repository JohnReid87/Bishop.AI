using Bishop.App.Batches.AbandonBatch;
using Bishop.App.Batches.CleanUpBatch;
using Bishop.App.Batches.CreateBatch;
using Bishop.App.Batches.LaunchBatchTerminal;
using Bishop.App.Batches.ListBatches;
using Bishop.App.Batches.MergeBatch;
using Bishop.App.Batches.RemoveBatch;
using Bishop.App.Batches.RenameBatch;
using Bishop.App.Batches.RequestStopBatch;
using Bishop.App.Services.Terminal;
using Bishop.App.Skills;
using Bishop.App.Tags.ListTags;
using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels.Workspaces;

public sealed partial class WorkspaceBatchesViewModel : ObservableObject
{
    private readonly ISender _mediator;
    private Guid _workspaceId;
    private string _workspacePath = string.Empty;

    public ObservableCollection<BatchItemViewModel> Batches { get; } = [];

    [ObservableProperty]
    private bool _hasBatches;

    [ObservableProperty]
    private bool _hasClosedBatches;

    [ObservableProperty]
    private int _closedBatchCount;

    [ObservableProperty]
    private int _badgeCount;

    [ObservableProperty]
    private string _badgeColor = string.Empty;

    [ObservableProperty]
    private bool _badgeIsVisible;

    [ObservableProperty]
    private string _badgeTooltip = string.Empty;

    public WorkspaceBatchesViewModel(ISender mediator)
    {
        _mediator = mediator;
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
        var summaries = await _mediator.Send(new ListBatchesQuery(_workspaceId, _workspacePath));
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
                    GitHubIssueNumber = card.GitHubIssueNumber,
                    GitHubPushedAt = card.GitHubPushedAt,
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
        ClosedBatchCount = Batches.Count(b => b.Status == BatchStatus.Closed);
        HasClosedBatches = ClosedBatchCount > 0;
        UpdateBadge();
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

    public async Task AbandonAsync(string batchName, string workspacePath)
        => await _mediator.Send(new AbandonBatchCommand(batchName, workspacePath));

    public async Task RemoveAsync(string batchName)
        => await _mediator.Send(new RemoveBatchCommand(batchName));

    public async Task RemoveAllClosedAsync(IReadOnlyList<BatchItemViewModel> closed)
    {
        foreach (var batch in closed)
            await _mediator.Send(new RemoveBatchCommand(batch.Name));
    }

    public async Task<string> RenameAsync(string oldName, string newName)
    {
        var renamed = await _mediator.Send(new RenameBatchCommand(oldName, newName));
        return renamed.Name;
    }

    public async Task CreateAsync(Guid workspaceId, string workspacePath, string name,
        string branchName, string worktreePath, int[] cardNumbers, string model = SkillModelOptions.DefaultModelId)
        => await _mediator.Send(new CreateBatchCommand(
            workspaceId, workspacePath, name, branchName, null, worktreePath, cardNumbers, null, null, model));

    public async Task LaunchBatch(string workspacePath, string batchName, string model, TerminalSnap snap)
        => await _mediator.Send(new LaunchBatchTerminalCommand(workspacePath, batchName, model, Resume: false, snap));

    public async Task ResumeBatch(string workspacePath, string batchName, string model, TerminalSnap snap)
        => await _mediator.Send(new LaunchBatchTerminalCommand(workspacePath, batchName, model, Resume: true, snap));
}

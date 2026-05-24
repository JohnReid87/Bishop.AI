using Bishop.App.Batches.ListBatches;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Collections.ObjectModel;

namespace Bishop.ViewModels;

public sealed partial class WorkspaceBatchesViewModel : ObservableObject
{
    private readonly ISender _mediator;

    public ObservableCollection<BatchItemViewModel> Batches { get; } = [];

    [ObservableProperty]
    private bool _hasBatches;

    public WorkspaceBatchesViewModel(ISender mediator) => _mediator = mediator;

    public async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var summaries = await _mediator.Send(new ListBatchesQuery());
        Batches.Clear();
        foreach (var s in summaries)
            Batches.Add(new BatchItemViewModel
            {
                Id = s.Batch.Id,
                Name = s.Batch.Name,
                BranchName = s.Batch.BranchName,
                Status = s.Batch.Status,
                CardCount = s.CardCount,
            });
        HasBatches = Batches.Count > 0;
    }
}

using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using System.Collections.ObjectModel;

namespace Bishop.Tests.ViewModels.Workspaces;

public class LaneItemsBuilderTests
{
    // --- ReconcileItems ---

    [Fact]
    public void ReconcileItems_EmptyWanted_ClearsCollection()
    {
        var collection = new ObservableCollection<object> { new(), new() };

        LaneItemsBuilder.ReconcileItems(collection, new List<object>());

        collection.Should().BeEmpty();
    }

    [Fact]
    public void ReconcileItems_EmptyCollection_AddsAllWanted()
    {
        var a = new object();
        var b = new object();
        var collection = new ObservableCollection<object>();

        LaneItemsBuilder.ReconcileItems(collection, [a, b]);

        collection.Should().Equal(a, b);
    }

    [Fact]
    public void ReconcileItems_SameReferences_EmitsNoCollectionChangedEvents()
    {
        var a = new object();
        var b = new object();
        var collection = new ObservableCollection<object> { a, b };
        var changes = 0;
        collection.CollectionChanged += (_, _) => changes++;

        LaneItemsBuilder.ReconcileItems(collection, [a, b]);

        changes.Should().Be(0);
    }

    [Fact]
    public void ReconcileItems_LongerWanted_AppendsNewItems()
    {
        var a = new object();
        var b = new object();
        var collection = new ObservableCollection<object> { a };

        LaneItemsBuilder.ReconcileItems(collection, [a, b]);

        collection.Should().Equal(a, b);
    }

    [Fact]
    public void ReconcileItems_ShorterWanted_RemovesTrailingItems()
    {
        var a = new object();
        var b = new object();
        var collection = new ObservableCollection<object> { a, b };

        LaneItemsBuilder.ReconcileItems(collection, [a]);

        collection.Should().Equal([a]);
    }

    [Fact]
    public void ReconcileItems_DifferentReferenceAtIndex_ReplacesItem()
    {
        var a = new object();
        var b = new object();
        var collection = new ObservableCollection<object> { a };

        LaneItemsBuilder.ReconcileItems(collection, [b]);

        collection[0].Should().BeSameAs(b);
    }

    // --- Build ---

    [Fact]
    public void Build_StandaloneCards_AppearDirectlyInTarget()
    {
        var cards = new List<CardViewModel> { new() { Title = "A" }, new() { Title = "B" } };

        var (activeGroups, _, target) = LaneItemsBuilder.Build(cards, new Dictionary<Guid, BatchStats>(), new Dictionary<Guid, BatchGroupViewModel>());

        activeGroups.Should().BeEmpty();
        target.Should().HaveCount(2);
        target[0].Should().BeSameAs(cards[0]);
        target[1].Should().BeSameAs(cards[1]);
    }

    [Fact]
    public void Build_BatchCards_AreGroupedUnderSingleEntry()
    {
        var batchId = Guid.NewGuid();
        var cards = new List<CardViewModel>
        {
            new() { Title = "C1", BatchId = batchId },
            new() { Title = "C2", BatchId = batchId }
        };

        var (activeGroups, groupCards, target) = LaneItemsBuilder.Build(cards, new Dictionary<Guid, BatchStats>(), new Dictionary<Guid, BatchGroupViewModel>());

        activeGroups.Should().ContainKey(batchId);
        groupCards[batchId].Should().HaveCount(2);
        target.Should().ContainSingle().Which.Should().BeOfType<BatchGroupViewModel>();
    }

    [Fact]
    public void Build_BatchStats_AreAppliedToGroup()
    {
        var batchId = Guid.NewGuid();
        var cards = new List<CardViewModel> { new() { Title = "C1", BatchId = batchId } };
        var stats = new Dictionary<Guid, BatchStats> { [batchId] = new("Sprint 1", 5, 2, 1) };

        var (activeGroups, _, _) = LaneItemsBuilder.Build(cards, stats, new Dictionary<Guid, BatchGroupViewModel>());

        var group = activeGroups[batchId];
        group.BatchName.Should().Be("Sprint 1");
        group.TotalCount.Should().Be(5);
        group.DoneCount.Should().Be(2);
        group.AccentIndex.Should().Be(1);
    }

    [Fact]
    public void Build_NoStats_FallsBackToCardBatchName()
    {
        var batchId = Guid.NewGuid();
        var cards = new List<CardViewModel> { new() { Title = "C1", BatchId = batchId, BatchName = "FallbackName" } };

        var (activeGroups, _, _) = LaneItemsBuilder.Build(cards, new Dictionary<Guid, BatchStats>(), new Dictionary<Guid, BatchGroupViewModel>());

        activeGroups[batchId].BatchName.Should().Be("FallbackName");
    }

    [Fact]
    public void Build_NullBatchName_FallsBackToEmptyString()
    {
        var batchId = Guid.NewGuid();
        var cards = new List<CardViewModel> { new() { Title = "C1", BatchId = batchId, BatchName = null } };

        var (activeGroups, _, _) = LaneItemsBuilder.Build(cards, new Dictionary<Guid, BatchStats>(), new Dictionary<Guid, BatchGroupViewModel>());

        activeGroups[batchId].BatchName.Should().BeEmpty();
    }

    [Fact]
    public void Build_ExistingGroup_IsReusedByReference()
    {
        var batchId = Guid.NewGuid();
        var existing = new BatchGroupViewModel { BatchId = batchId };
        var cards = new List<CardViewModel> { new() { Title = "C1", BatchId = batchId } };
        var existingGroups = new Dictionary<Guid, BatchGroupViewModel> { [batchId] = existing };

        var (activeGroups, _, _) = LaneItemsBuilder.Build(cards, new Dictionary<Guid, BatchStats>(), existingGroups);

        activeGroups[batchId].Should().BeSameAs(existing);
    }

    [Fact]
    public void Build_MultipleBatches_EachGetOwnGroup()
    {
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();
        var cards = new List<CardViewModel>
        {
            new() { Title = "A1", BatchId = batchA },
            new() { Title = "B1", BatchId = batchB },
            new() { Title = "A2", BatchId = batchA }
        };

        var (activeGroups, groupCards, target) = LaneItemsBuilder.Build(cards, new Dictionary<Guid, BatchStats>(), new Dictionary<Guid, BatchGroupViewModel>());

        activeGroups.Should().HaveCount(2);
        groupCards[batchA].Should().HaveCount(2);
        groupCards[batchB].Should().HaveCount(1);
        target.Should().HaveCount(2);
    }
}

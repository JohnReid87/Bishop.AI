using Bishop.Core;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Workspaces;

public class BoardCardFactoryTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyColours =
        new Dictionary<string, string>();

    private static readonly IReadOnlyDictionary<string, string> TagColours =
        new Dictionary<string, string> { ["feature"] = "#7fa87a", ["bug"] = "#c97a8a" };

    private static Card MakeCard() => new()
    {
        Id = Guid.NewGuid(),
        Number = 42,
        Title = "Test card",
        Description = "Some description",
        LaneName = "To Do",
        IsClosed = false,
    };

    // --- Build ---

    [Fact]
    public void Build_MapsAllScalarFields()
    {
        var card = MakeCard();
        card.BatchId = Guid.NewGuid();

        var vm = BoardCardFactory.Build(card, "To Do", EmptyColours, isSkillsButtonVisible: true);

        vm.Id.Should().Be(card.Id);
        vm.Number.Should().Be(card.Number);
        vm.Title.Should().Be(card.Title);
        vm.Description.Should().Be(card.Description);
        vm.LaneName.Should().Be("To Do");
        vm.IsClosed.Should().BeFalse();
        vm.BatchId.Should().Be(card.BatchId);
        vm.IsSkillsButtonVisible.Should().BeTrue();
    }

    [Fact]
    public void Build_WithNullBatch_LeavesBatchFieldsNull()
    {
        var card = MakeCard();

        var vm = BoardCardFactory.Build(card, "To Do", EmptyColours, isSkillsButtonVisible: false);

        vm.BatchName.Should().BeNull();
        vm.BatchCreatedAt.Should().BeNull();
    }

    [Fact]
    public void Build_WithBatch_MapsBatchNameAndCreatedAt()
    {
        var batchCreatedAt = DateTimeOffset.UtcNow;
        var card = MakeCard();
        card.Batch = new Batch { Name = "sprint-1", CreatedAt = batchCreatedAt };

        var vm = BoardCardFactory.Build(card, "To Do", EmptyColours, isSkillsButtonVisible: false);

        vm.BatchName.Should().Be("sprint-1");
        vm.BatchCreatedAt.Should().Be(batchCreatedAt);
    }

    [Fact]
    public void Build_WithKnownTag_ResolvesTagColour()
    {
        var card = MakeCard();
        card.TagName = "feature";

        var vm = BoardCardFactory.Build(card, "To Do", TagColours, isSkillsButtonVisible: false);

        vm.TagName.Should().Be("feature");
        vm.TagColour.Should().Be("#7fa87a");
    }

    [Fact]
    public void Build_WithNullTag_LeavesTagColourNull()
    {
        var card = MakeCard();

        var vm = BoardCardFactory.Build(card, "To Do", TagColours, isSkillsButtonVisible: false);

        vm.TagName.Should().BeNull();
        vm.TagColour.Should().BeNull();
    }

    // --- Matches ---

    [Fact]
    public void Matches_IdenticalCard_ReturnsTrue()
    {
        var card = MakeCard();
        card.TagName = "feature";
        var vm = BoardCardFactory.Build(card, "To Do", TagColours, isSkillsButtonVisible: false);

        BoardCardFactory.Matches(vm, card, TagColours).Should().BeTrue();
    }

    [Fact]
    public void Matches_DifferentTitle_ReturnsFalse()
    {
        var card = MakeCard();
        var vm = BoardCardFactory.Build(card, "To Do", EmptyColours, isSkillsButtonVisible: false);
        card.Title = "Changed";

        BoardCardFactory.Matches(vm, card, EmptyColours).Should().BeFalse();
    }

    [Fact]
    public void Matches_DifferentNullableField_ReturnsFalse()
    {
        var card = MakeCard();
        var vm = BoardCardFactory.Build(card, "To Do", EmptyColours, isSkillsButtonVisible: false);
        card.BatchId = Guid.NewGuid();

        BoardCardFactory.Matches(vm, card, EmptyColours).Should().BeFalse();
    }

    [Fact]
    public void Matches_DifferentTagName_ReturnsFalse()
    {
        var card = MakeCard();
        card.TagName = "feature";
        var vm = BoardCardFactory.Build(card, "To Do", TagColours, isSkillsButtonVisible: false);
        card.TagName = "bug";

        BoardCardFactory.Matches(vm, card, TagColours).Should().BeFalse();
    }

    [Fact]
    public void Matches_SameTagDifferentColourMap_ReturnsFalse()
    {
        var card = MakeCard();
        card.TagName = "feature";
        var vm = BoardCardFactory.Build(card, "To Do", TagColours, isSkillsButtonVisible: false);

        var differentColours = new Dictionary<string, string> { ["feature"] = "#ffffff" };
        BoardCardFactory.Matches(vm, card, differentColours).Should().BeFalse();
    }
}

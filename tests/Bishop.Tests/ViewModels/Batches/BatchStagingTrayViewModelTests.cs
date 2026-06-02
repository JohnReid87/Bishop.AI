using Bishop.App.Skills;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Batches;

public class BatchStagingTrayViewModelTests
{
    [Fact]
    public void IsVisible_FalseWhenNoCards()
    {
        var vm = new BatchStagingTrayViewModel();

        vm.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void IsVisible_TrueWhenCardsPresent()
    {
        var vm = new BatchStagingTrayViewModel();
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });

        vm.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Branch_AutoDerivesFromName()
    {
        var vm = new BatchStagingTrayViewModel();

        vm.Name = "My New Batch";

        vm.Branch.Should().Be("bishop/my-new-batch");
    }

    [Fact]
    public void Branch_StopsAutoDerivingAfterManualEdit()
    {
        var vm = new BatchStagingTrayViewModel();
        vm.Name = "First";
        vm.Branch.Should().Be("bishop/first");

        vm.Branch = "custom/branch";
        vm.Name = "Second";

        vm.Branch.Should().Be("custom/branch");
    }

    [Fact]
    public void Model_DefaultsToSkillModelDefault()
    {
        var vm = new BatchStagingTrayViewModel();

        vm.Model.Should().Be(SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public void Models_ListsAllSkillModels()
    {
        var vm = new BatchStagingTrayViewModel();

        vm.Models.Should().BeEquivalentTo(SkillModels.All);
    }

    [Fact]
    public void CreateLabel_IncludesCardCount()
    {
        var vm = new BatchStagingTrayViewModel();
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });
        vm.Cards.Add(new CardViewModel { Title = "Beta" });

        vm.CreateLabel.Should().Be("Create (2)");
    }

    [Fact]
    public void CanCreate_FalseWhenNoCards()
    {
        var vm = new BatchStagingTrayViewModel { Name = "x" };

        vm.CanCreate.Should().BeFalse();
    }

    [Fact]
    public void CanCreate_FalseWhenNameBlank()
    {
        var vm = new BatchStagingTrayViewModel();
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });

        vm.CanCreate.Should().BeFalse();
    }

    [Fact]
    public void CanCreate_TrueWhenNameAndCardsPresent()
    {
        var vm = new BatchStagingTrayViewModel { Name = "ok" };
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });

        vm.CanCreate.Should().BeTrue();
    }

    [Fact]
    public void Reset_ClearsFieldsAndCards()
    {
        var vm = new BatchStagingTrayViewModel
        {
            Name = "n",
            Model = "claude-opus-4-8",
            BaseBranch = "main",
        };
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });

        vm.Reset();

        vm.Cards.Should().BeEmpty();
        vm.Name.Should().BeEmpty();
        vm.Branch.Should().BeEmpty();
        vm.Model.Should().Be(SkillModelOptions.DefaultModelId);
        vm.BaseBranch.Should().BeEmpty();
    }

    [Fact]
    public void Reset_RestoresAutoDeriveBehaviour()
    {
        var vm = new BatchStagingTrayViewModel();
        vm.Name = "first";
        vm.Branch = "custom";
        vm.Reset();

        vm.Name = "second";

        vm.Branch.Should().Be("bishop/second");
    }

    [Fact]
    public void IsVisible_PropertyChangedFires_WhenCardAdded()
    {
        var vm = new BatchStagingTrayViewModel();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.Cards.Add(new CardViewModel { Title = "Alpha" });

        changes.Should().Contain(nameof(BatchStagingTrayViewModel.IsVisible));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.CreateLabel));
    }

    [Fact]
    public void AllDerivedProperties_PropertyChangedFires_WhenNameChanges()
    {
        var vm = new BatchStagingTrayViewModel();
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.Name = "New Name";

        changes.Should().Contain(nameof(BatchStagingTrayViewModel.IsVisible));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.Count));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.CreateLabel));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.CanCreate));
    }

    [Fact]
    public void AllDerivedProperties_PropertyChangedFires_OnReset()
    {
        var vm = new BatchStagingTrayViewModel { Name = "n" };
        vm.Cards.Add(new CardViewModel { Title = "Alpha" });
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.Reset();

        changes.Should().Contain(nameof(BatchStagingTrayViewModel.IsVisible));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.Count));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.CreateLabel));
        changes.Should().Contain(nameof(BatchStagingTrayViewModel.CanCreate));
    }

    [Theory]
    [InlineData("Plain", "bishop/plain")]
    [InlineData("With Spaces", "bishop/with-spaces")]
    [InlineData("Sym!Bols@#", "bishop/symbols")]
    [InlineData("MiXeD CaSe", "bishop/mixed-case")]
    public void Branch_SlugifiesName(string name, string expected)
    {
        var vm = new BatchStagingTrayViewModel();

        vm.Name = name;

        vm.Branch.Should().Be(expected);
    }
}

using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;

namespace Bishop.Tests.ViewModels.Cards;

public class CardViewModelTests
{
    [Fact]
    public void NumberDisplay_PrefixesWithHash()
    {
        var vm = new CardViewModel { Number = 42 };

        vm.NumberDisplay.Should().Be("#42");
    }

    [Fact]
    public void IsTagVisible_TrueWhenTagNamePresent()
    {
        var withTag = new CardViewModel { TagName = "bug" };
        var withoutTag = new CardViewModel { TagName = null };

        withTag.IsTagVisible.Should().BeTrue();
        withoutTag.IsTagVisible.Should().BeFalse();
    }

    [Fact]
    public void IsAddTagButtonVisible_TrueWhenTagNameMissing()
    {
        var withTag = new CardViewModel { TagName = "bug" };
        var withoutTag = new CardViewModel { TagName = null };

        withTag.IsAddTagButtonVisible.Should().BeFalse();
        withoutTag.IsAddTagButtonVisible.Should().BeTrue();
    }

    [Fact]
    public void CardOpacity_HalvedWhenClosed()
    {
        var open = new CardViewModel { IsClosed = false };
        var closed = new CardViewModel { IsClosed = true };

        open.CardOpacity.Should().Be(1.0);
        closed.CardOpacity.Should().Be(0.5);
    }

    [Fact]
    public void CloseReopenTooltip_SwitchesOnClosedState()
    {
        var open = new CardViewModel { IsClosed = false };
        var closed = new CardViewModel { IsClosed = true };

        open.CloseReopenTooltip.Should().Be("Close card");
        closed.CloseReopenTooltip.Should().Be("Reopen card");
    }

    [Theory]
    [InlineData("Backlog", false)]
    [InlineData("To Do", false)]
    [InlineData("Doing", false)]
    [InlineData("Done", true)]
    public void IsDoneLane_TrueOnlyForDoneLane(string laneName, bool expected)
    {
        var vm = new CardViewModel { LaneName = laneName };

        vm.IsDoneLane.Should().Be(expected);
    }

    [Fact]
    public void CardTitleFontSize_ShrinksOnDoneLane()
    {
        var inDone = new CardViewModel { LaneName = SystemLaneNames.Done };
        var inToDo = new CardViewModel { LaneName = SystemLaneNames.ToDo };

        inDone.CardTitleFontSize.Should().Be(12.0);
        inToDo.CardTitleFontSize.Should().Be(14.0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsSkillsButtonVisible_ReflectsInitValue(bool visible)
    {
        var vm = new CardViewModel { IsSkillsButtonVisible = visible };

        vm.IsSkillsButtonVisible.Should().Be(visible);
    }

    [Theory]
    [InlineData("Add lane CRUD", "lane", true)]
    [InlineData("Add lane CRUD", "LANE", true)]
    [InlineData("Add lane CRUD", "missing", false)]
    public void MatchesSearch_LooksInTitleCaseInsensitively(string title, string search, bool expected)
    {
        var vm = new CardViewModel { Title = title };

        vm.MatchesSearch(search).Should().Be(expected);
    }

    [Fact]
    public void MatchesSearch_AlsoMatchesTagName()
    {
        var vm = new CardViewModel { Title = "Refactor", TagName = "tech-debt" };

        vm.MatchesSearch("tech").Should().BeTrue();
    }

    [Fact]
    public void MatchesSearch_HandlesNullTag()
    {
        var vm = new CardViewModel { Title = "Refactor", TagName = null };

        vm.MatchesSearch("Refactor").Should().BeTrue();
        vm.MatchesSearch("tech").Should().BeFalse();
    }

    [Fact]
    public void CloseReopenGlyph_ReturnsStringForEachClosedState()
    {
        var open = new CardViewModel { IsClosed = false };
        var closed = new CardViewModel { IsClosed = true };

        open.CloseReopenGlyph.Should().NotBeNull();
        closed.CloseReopenGlyph.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Any title", null)]
    [InlineData("", null)]
    [InlineData("Any title", "tag")]
    [InlineData("", "")]
    public void MatchesSearch_EmptyQueryMatchesAnyCard(string title, string? tagName)
    {
        var vm = new CardViewModel { Title = title, TagName = tagName };

        vm.MatchesSearch("").Should().BeTrue();
    }

    [Fact]
    public void MatchesSearch_EmptyTitleDoesNotMatchNonEmptyQuery()
    {
        var vm = new CardViewModel { Title = "" };

        vm.MatchesSearch("anything").Should().BeFalse();
    }

    [Fact]
    public void MatchesSearch_EmptyTagNameDoesNotContributeToMatch()
    {
        var vm = new CardViewModel { Title = "irrelevant", TagName = "" };

        vm.MatchesSearch("query").Should().BeFalse();
    }

    [Theory]
    [InlineData(273, "27", true)]
    [InlineData(27, "27", true)]
    [InlineData(270, "27", true)]
    [InlineData(100, "27", false)]
    public void MatchesSearch_MatchesNumberSubstring(int number, string search, bool expected)
    {
        var vm = new CardViewModel { Number = number };

        vm.MatchesSearch(search).Should().Be(expected);
    }

    [Theory]
    [InlineData("#273", true)]
    [InlineData("273", true)]
    public void MatchesSearch_StripsSingleLeadingHashBeforeNumberMatch(string search, bool expected)
    {
        var vm = new CardViewModel { Number = 273 };

        vm.MatchesSearch(search).Should().Be(expected);
    }

    [Fact]
    public void MatchesSearch_MatchesDescriptionSubstring()
    {
        var vm = new CardViewModel { Title = "irrelevant", Description = "some detailed description text" };

        vm.MatchesSearch("detailed").Should().BeTrue();
    }

    [Fact]
    public void MatchesSearch_NoMatchWhenQueryAbsentFromAllFields()
    {
        var vm = new CardViewModel { Number = 42, Title = "Add lane CRUD", TagName = "feature", Description = "Create the lane endpoints." };

        vm.MatchesSearch("xyz-not-present").Should().BeFalse();
    }

    [Theory]
    [InlineData("Backlog")]
    [InlineData("To Do")]
    [InlineData("Done")]
    [InlineData("Doing")]
    public void IsAutoRunFailedIndicatorVisible_TrueInAnyLaneWhenFieldSet(string laneName)
    {
        var vm = new CardViewModel { LaneName = laneName, LastAutoRunFailedAt = DateTimeOffset.UtcNow };

        vm.IsAutoRunFailedIndicatorVisible.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRunFailedIndicatorVisible_FalseWhenFieldNotSet()
    {
        var vm = new CardViewModel { LaneName = SystemLaneNames.Doing, LastAutoRunFailedAt = null };

        vm.IsAutoRunFailedIndicatorVisible.Should().BeFalse();
    }

    [Fact]
    public void AutoRunFailedTooltip_ContainsFormattedTimestampWhenFieldSet()
    {
        var timestamp = new DateTimeOffset(2026, 5, 24, 14, 30, 0, TimeSpan.Zero);
        var vm = new CardViewModel { LastAutoRunFailedAt = timestamp };

        vm.AutoRunFailedTooltip.Should().Be("Auto-run failed at 2026-05-24 14:30");
    }

    [Fact]
    public void AutoRunFailedTooltip_EmptyWhenFieldNotSet()
    {
        var vm = new CardViewModel { LastAutoRunFailedAt = null };

        vm.AutoRunFailedTooltip.Should().BeEmpty();
    }

    [Fact]
    public void IsSelected_DefaultsFalse()
    {
        var vm = new CardViewModel();

        vm.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void IsSelected_CanBeSetToTrue()
    {
        var vm = new CardViewModel();

        vm.IsSelected = true;

        vm.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void IsSelected_RaisesPropertyChangedNotification()
    {
        var vm = new CardViewModel();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsSelected = true;

        changed.Should().Contain(nameof(CardViewModel.IsSelected));
    }

    [Theory]
    [InlineData("Done", "#5da75d")]
    [InlineData("Doing", "#c4904a")]
    [InlineData("To Do", "#5a8ab8")]
    [InlineData("Backlog", "#66667a")]
    public void LaneDotColour_ReturnsCorrectColourForLane(string laneName, string expected)
    {
        var vm = new CardViewModel { LaneName = laneName };

        vm.LaneDotColour.Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsInProgress_ReflectsInitValue(bool inProgress)
    {
        var vm = new CardViewModel { IsInProgress = inProgress };

        vm.IsInProgress.Should().Be(inProgress);
    }

    [Fact]
    public void IsAutoRunFailedIndicatorVisible_FalseWhenSucceededAfterFailed()
    {
        var failed = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var succeeded = new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
        var vm = new CardViewModel { LastAutoRunFailedAt = failed, LastAutoRunSucceededAt = succeeded };

        vm.IsAutoRunFailedIndicatorVisible.Should().BeFalse();
    }

    [Fact]
    public void IsAutoRunFailedIndicatorVisible_TrueWhenFailedAfterSucceeded()
    {
        var succeeded = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var failed = new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
        var vm = new CardViewModel { LastAutoRunFailedAt = failed, LastAutoRunSucceededAt = succeeded };

        vm.IsAutoRunFailedIndicatorVisible.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRunSucceededIndicatorVisible_TrueWhenOnlySucceeded()
    {
        var vm = new CardViewModel { LastAutoRunSucceededAt = DateTimeOffset.UtcNow };

        vm.IsAutoRunSucceededIndicatorVisible.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRunSucceededIndicatorVisible_FalseWhenFieldNotSet()
    {
        var vm = new CardViewModel { LastAutoRunSucceededAt = null };

        vm.IsAutoRunSucceededIndicatorVisible.Should().BeFalse();
    }

    [Fact]
    public void IsAutoRunSucceededIndicatorVisible_TrueWhenSucceededAfterFailed()
    {
        var failed = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var succeeded = new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
        var vm = new CardViewModel { LastAutoRunFailedAt = failed, LastAutoRunSucceededAt = succeeded };

        vm.IsAutoRunSucceededIndicatorVisible.Should().BeTrue();
    }

    [Fact]
    public void IsAutoRunSucceededIndicatorVisible_FalseWhenFailedAfterSucceeded()
    {
        var succeeded = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        var failed = new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
        var vm = new CardViewModel { LastAutoRunFailedAt = failed, LastAutoRunSucceededAt = succeeded };

        vm.IsAutoRunSucceededIndicatorVisible.Should().BeFalse();
    }

    [Fact]
    public void AutoRunSucceededTooltip_ContainsFormattedTimestampWhenFieldSet()
    {
        var timestamp = new DateTimeOffset(2026, 5, 24, 14, 30, 0, TimeSpan.Zero);
        var vm = new CardViewModel { LastAutoRunSucceededAt = timestamp };

        vm.AutoRunSucceededTooltip.Should().Be("Auto-run succeeded at 2026-05-24 14:30");
    }

    [Fact]
    public void AutoRunSucceededTooltip_EmptyWhenFieldNotSet()
    {
        var vm = new CardViewModel { LastAutoRunSucceededAt = null };

        vm.AutoRunSucceededTooltip.Should().BeEmpty();
    }

    [Fact]
    public void IsClosed_DefaultsFalse()
    {
        var vm = new CardViewModel();

        vm.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void IsClosed_CanBeSetToTrue()
    {
        var vm = new CardViewModel();

        vm.IsClosed = true;

        vm.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void IsClosed_RaisesPropertyChangedNotification()
    {
        var vm = new CardViewModel();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsClosed = true;

        changed.Should().Contain(nameof(CardViewModel.IsClosed));
    }

    [Fact]
    public void IsClosed_RaisesPropertyChangedForCardOpacity()
    {
        var vm = new CardViewModel();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsClosed = true;

        changed.Should().Contain(nameof(CardViewModel.CardOpacity));
    }

    [Fact]
    public void IsClosed_RaisesPropertyChangedForCloseReopenGlyph()
    {
        var vm = new CardViewModel();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsClosed = true;

        changed.Should().Contain(nameof(CardViewModel.CloseReopenGlyph));
    }

    [Fact]
    public void IsClosed_RaisesPropertyChangedForCloseReopenTooltip()
    {
        var vm = new CardViewModel();
        var changed = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsClosed = true;

        changed.Should().Contain(nameof(CardViewModel.CloseReopenTooltip));
    }
}

using System.ComponentModel;
using Bishop.App.CatMode;
using FluentAssertions;

namespace Bishop.Tests.App.CatMode;

public sealed class CatModeServiceTests
{
    [Fact]
    public void IsActive_IsFalse_OnInitialState()
    {
        // Arrange
        var sut = new CatModeService();

        // Assert
        sut.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Toggle_FlipsIsActiveFromFalseToTrue()
    {
        // Arrange
        var sut = new CatModeService();

        // Act
        sut.Toggle();

        // Assert
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Toggle_FlipsIsActiveFromTrueToFalse()
    {
        // Arrange
        var sut = new CatModeService();
        sut.Toggle();

        // Act
        sut.Toggle();

        // Assert
        sut.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Toggle_RaisesPropertyChangedForIsActive()
    {
        // Arrange
        var sut = new CatModeService();
        var changes = new List<string?>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        // Act
        sut.Toggle();

        // Assert
        changes.Should().ContainSingle().Which.Should().Be(nameof(ICatModeService.IsActive));
    }

    [Fact]
    public void Toggle_RaisesPropertyChangedOncePerToggle()
    {
        // Arrange
        var sut = new CatModeService();
        var count = 0;
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, _) => count++;

        // Act
        sut.Toggle();
        sut.Toggle();
        sut.Toggle();

        // Assert
        count.Should().Be(3);
    }
}

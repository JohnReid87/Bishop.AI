using Bishop.App.Services;
using FluentAssertions;

namespace Bishop.Tests.App.Services;

public sealed class WorkspaceChangeNotifierTests
{
    [Fact]
    public void NotifyChanged_RaisesWorkspacesChangedEvent()
    {
        // Arrange
        var sut = new WorkspaceChangeNotifier();
        var fired = false;
        sut.WorkspacesChanged += () => fired = true;

        // Act
        sut.NotifyChanged();

        // Assert
        fired.Should().BeTrue();
    }

    [Fact]
    public void NotifyChanged_DoesNotThrow_WhenNoSubscribers()
    {
        // Arrange
        var sut = new WorkspaceChangeNotifier();

        // Act
        var act = () => sut.NotifyChanged();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void NotifyChanged_InvokesAllSubscribers()
    {
        // Arrange
        var sut = new WorkspaceChangeNotifier();
        var count = 0;
        sut.WorkspacesChanged += () => count++;
        sut.WorkspacesChanged += () => count++;

        // Act
        sut.NotifyChanged();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void NotifyChanged_InvokesEventOncePerCall()
    {
        // Arrange
        var sut = new WorkspaceChangeNotifier();
        var count = 0;
        sut.WorkspacesChanged += () => count++;

        // Act
        sut.NotifyChanged();
        sut.NotifyChanged();
        sut.NotifyChanged();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void NotifyChanged_DoesNotFireAfterUnsubscribe()
    {
        // Arrange
        var sut = new WorkspaceChangeNotifier();
        var fired = false;
        Action handler = () => fired = true;
        sut.WorkspacesChanged += handler;
        sut.WorkspacesChanged -= handler;

        // Act
        sut.NotifyChanged();

        // Assert
        fired.Should().BeFalse();
    }
}

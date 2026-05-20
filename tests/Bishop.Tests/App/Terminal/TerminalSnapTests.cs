using Bishop.App.Terminal;
using FluentAssertions;

namespace Bishop.Tests.App.Terminal;

public sealed class TerminalSnapTests
{
    [Fact]
    public void RightHalf_SingleMonitor_ReturnsRightHalfRect()
    {
        // Act
        var snap = TerminalSnap.RightHalf(0, 0, 2560, 1440);

        // Assert
        snap.X.Should().Be(1280);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(1280);
        snap.Height.Should().Be(1440);
    }

    [Fact]
    public void RightHalf_WithDisplayOffset_IncludesOffset()
    {
        // Act — second monitor positioned at x=1920
        var snap = TerminalSnap.RightHalf(1920, 0, 2560, 1440);

        // Assert
        snap.X.Should().Be(3200); // 1920 + 1280
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(1280);
        snap.Height.Should().Be(1440);
    }

    [Fact]
    public void RightHalf_FullHdMonitor_ReturnsCorrectHalfWidth()
    {
        // Act
        var snap = TerminalSnap.RightHalf(0, 0, 1920, 1080);

        // Assert
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }
}

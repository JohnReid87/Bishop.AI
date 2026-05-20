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

    [Fact]
    public void RemainderFill_BishopOnLeftHalf_FillsRightHalf()
    {
        // Arrange — Bishop occupies left half of 1920×1080 work area
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 960, 1080, 0, 0, 1920, 1080);

        // Assert
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_BishopOnRightHalf_FillsLeftHalf()
    {
        // Arrange — Bishop occupies right half of 1920×1080 work area
        // Act
        var snap = TerminalSnap.RemainderFill(960, 0, 960, 1080, 0, 0, 1920, 1080);

        // Assert
        snap.X.Should().Be(0);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_BishopOnTopHalf_FillsBottomHalf()
    {
        // Arrange — Bishop occupies top half of 1920×1080 work area
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1920, 540, 0, 0, 1920, 1080);

        // Assert
        snap.X.Should().Be(0);
        snap.Y.Should().Be(540);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(540);
    }

    [Fact]
    public void RemainderFill_BishopOnBottomHalf_FillsTopHalf()
    {
        // Arrange — Bishop occupies bottom half of 1920×1080 work area
        // Act
        var snap = TerminalSnap.RemainderFill(0, 540, 1920, 540, 0, 0, 1920, 1080);

        // Assert
        snap.X.Should().Be(0);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(540);
    }

    [Fact]
    public void RemainderFill_BishopInTopLeftCorner_PicksLargerOfRightAndBottomStrips()
    {
        // Arrange — Bishop 960×360 in top-left; right strip = 960×1080 (1,036,800), bottom strip = 1920×720 (1,382,400)
        // Bottom has larger area, so it wins
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 960, 360, 0, 0, 1920, 1080);

        // Assert — bottom strip wins (area 1920×720 > 960×1080)
        snap.X.Should().Be(0);
        snap.Y.Should().Be(360);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(720);
    }

    [Fact]
    public void RemainderFill_BishopFullscreen_FallsBackToRightHalf()
    {
        // Arrange — Bishop covers entire work area
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1920, 1080, 0, 0, 1920, 1080);

        // Assert — all strips have 0 area; falls back to RightHalf
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_RemainderNarrowerThan400px_FallsBackToRightHalf()
    {
        // Arrange — Bishop leaves only a 300px-wide strip on the right
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1620, 1080, 0, 0, 1920, 1080);

        // Assert — right strip is 300px wide (< 400); falls back to RightHalf
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_WithDisplayOffset_ProducesOffsetAdjustedSnap()
    {
        // Arrange — second monitor at x=1920; Bishop on its left half
        // Act
        var snap = TerminalSnap.RemainderFill(1920, 0, 960, 1080, 1920, 0, 1920, 1080);

        // Assert — right strip starts at x=2880 (1920+960)
        snap.X.Should().Be(2880);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }
}

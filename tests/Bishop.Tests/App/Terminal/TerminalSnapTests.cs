using Bishop.App.Services.Terminal;
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

    // MinUsableDimension boundary tests — width axis (399 / 400 / 401)

    [Fact]
    public void RemainderFill_RightStripExactly399Wide_FallsBackToRightHalf()
    {
        // Arrange — Bishop leaves a 399px-wide right strip (bishopWidth = 1920 - 399 = 1521); all other strips are zero-sized
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1521, 1080, 0, 0, 1920, 1080);

        // Assert — right strip rejected (399 < 400); falls back to RightHalf
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_RightStripExactly400Wide_PicksRightStrip()
    {
        // Arrange — Bishop leaves a 400px-wide right strip (bishopWidth = 1920 - 400 = 1520)
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1520, 1080, 0, 0, 1920, 1080);

        // Assert — right strip accepted (400 == MinUsableDimension)
        snap.X.Should().Be(1520);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(400);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_RightStripExactly401Wide_PicksRightStrip()
    {
        // Arrange — Bishop leaves a 401px-wide right strip (bishopWidth = 1920 - 401 = 1519)
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1519, 1080, 0, 0, 1920, 1080);

        // Assert — right strip accepted (401 > MinUsableDimension)
        snap.X.Should().Be(1519);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(401);
        snap.Height.Should().Be(1080);
    }

    // MinUsableDimension boundary tests — height axis (399 / 400 / 401)

    [Fact]
    public void RemainderFill_BottomStripExactly399Tall_FallsBackToRightHalf()
    {
        // Arrange — Bishop (full width) leaves a 399px-tall bottom strip (bishopHeight = 1080 - 399 = 681)
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1920, 681, 0, 0, 1920, 1080);

        // Assert — bottom strip rejected (height 399 < 400); falls back to RightHalf
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_BottomStripExactly400Tall_PicksBottomStrip()
    {
        // Arrange — Bishop (full width) leaves a 400px-tall bottom strip (bishopHeight = 1080 - 400 = 680)
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1920, 680, 0, 0, 1920, 1080);

        // Assert — bottom strip accepted (400 == MinUsableDimension)
        snap.X.Should().Be(0);
        snap.Y.Should().Be(680);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(400);
    }

    [Fact]
    public void RemainderFill_BottomStripExactly401Tall_PicksBottomStrip()
    {
        // Arrange — Bishop (full width) leaves a 401px-tall bottom strip (bishopHeight = 1080 - 401 = 679)
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1920, 679, 0, 0, 1920, 1080);

        // Assert — bottom strip accepted (401 > MinUsableDimension)
        snap.X.Should().Be(0);
        snap.Y.Should().Be(679);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(401);
    }

    // Opposite-axis filter: wide enough (>= 400) but too short (< 400)

    [Fact]
    public void RemainderFill_RemainderWideEnoughButTooShort_FallsBackToRightHalf()
    {
        // Arrange — Bishop (full width) leaves a 199px-tall bottom strip; width passes but height fails
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 1920, 881, 0, 0, 1920, 1080);

        // Assert — bottom strip rejected (height 199 < 400 despite width 1920 >= 400); falls back to RightHalf
        snap.X.Should().Be(960);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(960);
        snap.Height.Should().Be(1080);
    }

    // Degenerate inputs — zero-sized Bishop and negative coordinates

    [Fact]
    public void RemainderFill_ZeroSizedBishopWindow_ReturnsFullWorkAreaAsRightStrip()
    {
        // Arrange — zero-width/height Bishop; the "right" strip spans the entire work area
        // Act
        var snap = TerminalSnap.RemainderFill(0, 0, 0, 0, 0, 0, 1920, 1080);

        // Assert — right strip (full work area 1920×1080) wins; terminal placed over the whole screen
        snap.X.Should().Be(0);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_BishopPartiallyOffLeftEdge_UsesAdjustedRightStrip()
    {
        // Arrange — Bishop starts at x=-100 (partially off screen); right strip is adjusted accordingly
        // Act
        var snap = TerminalSnap.RemainderFill(-100, 0, 960, 1080, 0, 0, 1920, 1080);

        // Assert — right strip starts at x=860 (-100+960) with width=1060 (1920-860)
        snap.X.Should().Be(860);
        snap.Y.Should().Be(0);
        snap.Width.Should().Be(1060);
        snap.Height.Should().Be(1080);
    }

    [Fact]
    public void RemainderFill_BishopPartiallyAboveTopEdge_UsesBottomStrip()
    {
        // Arrange — Bishop starts at y=-100 (partially above screen, full width); bottom strip is available
        // Act
        var snap = TerminalSnap.RemainderFill(0, -100, 1920, 600, 0, 0, 1920, 1080);

        // Assert — bottom strip starts at y=500 (-100+600) with height=580 (1080-500)
        snap.X.Should().Be(0);
        snap.Y.Should().Be(500);
        snap.Width.Should().Be(1920);
        snap.Height.Should().Be(580);
    }
}

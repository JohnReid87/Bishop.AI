using Bishop.App.Skills;
using Bishop.App.Terminal;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class SnapComputerTests
{
    // Work area: 1920x1080 at origin; Bishop window: 100px wide at x=0, so large right remainder
    private const int WaX = 0, WaY = 0, WaW = 1920, WaH = 1080;
    private const int BisX = 0, BisY = 0, BisW = 100, BisH = 1080;

    [Fact]
    public void Compute_WhenPhysicalBoundsAvailable_UsesPhysicalBoundsForSnap()
    {
        var expected = TerminalSnap.RemainderFill(BisX, BisY, BisW, BisH, WaX, WaY, WaW, WaH);

        var result = SnapComputer.Compute(
            BisX, BisY, BisW, BisH, physicalBoundsAvailable: true,
            logX: 999, logY: 999, logW: 999, logH: 999,
            WaX, WaY, WaW, WaH);

        result.Should().Be(expected);
    }

    [Fact]
    public void Compute_WhenPhysicalBoundsUnavailable_UsesLogicalBoundsForSnap()
    {
        var expected = TerminalSnap.RemainderFill(BisX, BisY, BisW, BisH, WaX, WaY, WaW, WaH);

        var result = SnapComputer.Compute(
            physX: 999, physY: 999, physW: 999, physH: 999, physicalBoundsAvailable: false,
            BisX, BisY, BisW, BisH,
            WaX, WaY, WaW, WaH);

        result.Should().Be(expected);
    }

    [Fact]
    public void Compute_WhenPhysicalAndLogicalDiffer_PhysicalBoundsWinWhenAvailable()
    {
        // Physical bounds at left edge; logical shifted right — physical should produce right-fill snap
        var physSnap = TerminalSnap.RemainderFill(0, 0, 200, 1080, WaX, WaY, WaW, WaH);
        var logSnap  = TerminalSnap.RemainderFill(10, 0, 200, 1080, WaX, WaY, WaW, WaH);

        var result = SnapComputer.Compute(
            physX: 0,  physY: 0, physW: 200, physH: 1080, physicalBoundsAvailable: true,
            logX: 10, logY: 0, logW: 200, logH: 1080,
            WaX, WaY, WaW, WaH);

        result.Should().Be(physSnap);
        result.Should().NotBe(logSnap);
    }

    [Fact]
    public void Compute_WhenPhysicalAndLogicalDiffer_LogicalBoundsWinWhenPhysicalUnavailable()
    {
        var logSnap = TerminalSnap.RemainderFill(10, 0, 200, 1080, WaX, WaY, WaW, WaH);

        var result = SnapComputer.Compute(
            physX: 0, physY: 0, physW: 200, physH: 1080, physicalBoundsAvailable: false,
            logX: 10, logY: 0, logW: 200, logH: 1080,
            WaX, WaY, WaW, WaH);

        result.Should().Be(logSnap);
    }
}

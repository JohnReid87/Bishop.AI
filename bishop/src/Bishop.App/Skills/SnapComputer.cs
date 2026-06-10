using Bishop.App.Services.Terminal;

namespace Bishop.App.Skills;

public static class SnapComputer
{
    public static TerminalSnap Compute(
        int physX, int physY, int physW, int physH, bool physicalBoundsAvailable,
        int logX, int logY, int logW, int logH,
        int waX, int waY, int waW, int waH)
    {
        return physicalBoundsAvailable
            ? TerminalSnap.RemainderFill(physX, physY, physW, physH, waX, waY, waW, waH)
            : TerminalSnap.RemainderFill(logX, logY, logW, logH, waX, waY, waW, waH);
    }
}

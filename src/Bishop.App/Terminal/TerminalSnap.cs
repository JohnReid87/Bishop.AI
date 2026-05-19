namespace Bishop.App.Terminal;

public readonly record struct TerminalSnap(int X, int Y, int Width, int Height)
{
    public static TerminalSnap RightHalf(int workAreaX, int workAreaY, int workAreaWidth, int workAreaHeight) =>
        new(workAreaX + workAreaWidth / 2, workAreaY, workAreaWidth / 2, workAreaHeight);
}

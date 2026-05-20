namespace Bishop.App.Terminal;

public readonly record struct TerminalSnap(int X, int Y, int Width, int Height)
{
    private const int MinUsableDimension = 400;

    public static TerminalSnap RightHalf(int workAreaX, int workAreaY, int workAreaWidth, int workAreaHeight) =>
        new(workAreaX + workAreaWidth / 2, workAreaY, workAreaWidth / 2, workAreaHeight);

    public static TerminalSnap RemainderFill(
        int bishopX, int bishopY, int bishopWidth, int bishopHeight,
        int workAreaX, int workAreaY, int workAreaWidth, int workAreaHeight)
    {
        TerminalSnap[] candidates =
        [
            new(workAreaX, workAreaY, bishopX - workAreaX, workAreaHeight),                                                           // left
            new(bishopX + bishopWidth, workAreaY, workAreaX + workAreaWidth - (bishopX + bishopWidth), workAreaHeight),               // right
            new(workAreaX, workAreaY, workAreaWidth, bishopY - workAreaY),                                                            // top
            new(workAreaX, bishopY + bishopHeight, workAreaWidth, workAreaY + workAreaHeight - (bishopY + bishopHeight)),             // bottom
        ];

        var best = candidates
            .Where(c => c.Width >= MinUsableDimension && c.Height >= MinUsableDimension)
            .OrderByDescending(c => (long)c.Width * c.Height)
            .Cast<TerminalSnap?>()
            .FirstOrDefault();

        return best ?? RightHalf(workAreaX, workAreaY, workAreaWidth, workAreaHeight);
    }
}

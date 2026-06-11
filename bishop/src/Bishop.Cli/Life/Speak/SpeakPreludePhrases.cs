namespace Bishop.Cli.Life.Speak;

internal static class SpeakPreludePhrases
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Morning — just pulling your board together.",
        "One moment, gathering yesterday's threads.",
        "Hold on, catching up on where we left off.",
        "Give me a second, lining up your stand-up.",
        "Right, let me get oriented.",
        "Just a moment — reading the room.",
    };

    public static string Pick(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return All[random.Next(All.Count)];
    }
}

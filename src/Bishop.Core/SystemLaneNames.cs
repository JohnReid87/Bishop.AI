namespace Bishop.Core;

public static class SystemLaneNames
{
    public const string Backlog = "Backlog";
    public const string ToDo = "To Do";
    public const string Doing = "Doing";
    public const string Done = "Done";

    public static readonly IReadOnlyList<string> All = [Backlog, ToDo, Doing, Done];
}

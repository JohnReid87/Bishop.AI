namespace Bishop.ViewModels;

public sealed class WorkspaceManagerItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool IsRemoved { get; init; }
    public DateTimeOffset? RemovedAt { get; init; }

    public string StatusText => IsRemoved
        ? $"removed {RemovedAt:yyyy-MM-dd}"
        : "active";
}

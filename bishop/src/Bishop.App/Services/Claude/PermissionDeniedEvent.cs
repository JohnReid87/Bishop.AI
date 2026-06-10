namespace Bishop.App.Services.Claude;

public sealed record PermissionDeniedEvent(string? Tool, string? Command, string? Message);

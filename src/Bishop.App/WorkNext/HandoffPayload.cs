namespace Bishop.App.WorkNext;

public sealed record HandoffPayload(
    IReadOnlyList<string> CommitBodyBullets,
    IReadOnlyList<string> TouchedFiles,
    string? Notes = null);

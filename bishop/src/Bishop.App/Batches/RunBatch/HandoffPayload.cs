using System.Text.Json.Serialization;

namespace Bishop.App.Batches.RunBatch;

internal sealed record HandoffPayload(
    [property: JsonPropertyName("commit_body_bullets")] IReadOnlyList<string> CommitBodyBullets,
    [property: JsonPropertyName("touched_files")] IReadOnlyList<string> TouchedFiles,
    [property: JsonPropertyName("notes")] string? Notes);

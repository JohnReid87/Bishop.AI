using System;

namespace Bishop.Life.Core;

/// <summary>
/// Test seam over <see cref="ClaudeSessionJsonlTailer"/> so controllers that
/// own a tailer (e.g. the bishop.life stand-up controller) can be exercised
/// without an on-disk JSONL file and a live FileSystemWatcher.
/// </summary>
public interface IClaudeSessionTailer : IDisposable
{
    event Action<string>? UserMessage;
    event Action<string>? AssistantText;
    event Action<ClaudeSessionJsonlTailer.ToolUseEvent>? ToolUse;

    void Start();
}

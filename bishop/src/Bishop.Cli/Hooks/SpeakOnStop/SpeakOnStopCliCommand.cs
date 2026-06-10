using Bishop.Cli.Life.Speak;
using System.CommandLine;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;

namespace Bishop.Cli.Hooks.SpeakOnStop;

[SupportedOSPlatform("windows10.0.10240.0")]
internal sealed class SpeakOnStopCliCommand : Command
{
    public SpeakOnStopCliCommand()
        : base("speak-on-stop", "Stop hook: speak the last assistant message aloud when the active skill is one of the opted-in bish-life-* skills")
    {
        this.SetHandler(async (context) =>
        {
            try
            {
                var payload = await Console.In.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(payload))
                    return;

                JsonNode? root;
                try { root = JsonNode.Parse(payload); }
                catch { return; }

                var transcriptPath = root?["transcript_path"]?.GetValue<string>();
                if (string.IsNullOrEmpty(transcriptPath))
                    return;

                if (!BishLifeTranscriptScanner.TryGetTextToSpeak(transcriptPath, out var text))
                    return;

                await Speaker.SpeakAsync(text, context.GetCancellationToken());
            }
            catch (Exception ex)
            {
                // Stop hook must never throw — Claude Code only reads stdout, so a stderr line is diagnostic without blocking the conversation.
                await Console.Error.WriteLineAsync($"speak-on-stop: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }
}

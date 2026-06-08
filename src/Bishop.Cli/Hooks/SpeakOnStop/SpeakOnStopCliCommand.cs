using Bishop.Cli.Life.Speak;
using System.CommandLine;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;

namespace Bishop.Cli.Hooks.SpeakOnStop;

[SupportedOSPlatform("windows10.0.10240.0")]
internal sealed class SpeakOnStopCliCommand : Command
{
    public SpeakOnStopCliCommand()
        : base("speak-on-stop", "Stop hook: speak the last assistant message aloud when the active skill is bish-life-standup")
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

                if (!StandupTranscriptScanner.TryGetTextToSpeak(transcriptPath, out var text))
                    return;

                await WindowsSpeechSynthesizer.SpeakAsync(text, context.GetCancellationToken());
            }
            catch
            {
                // Silent failure — the card requires the hook to never block the conversation.
            }
        });
    }
}

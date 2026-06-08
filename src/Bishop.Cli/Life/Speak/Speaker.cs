using System.Runtime.Versioning;

namespace Bishop.Cli.Life.Speak;

[SupportedOSPlatform("windows10.0.10240.0")]
internal static class Speaker
{
    public static Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        => PiperSpeechSynthesizer.IsConfigured
            ? PiperSpeechSynthesizer.SpeakAsync(text, cancellationToken)
            : WindowsSpeechSynthesizer.SpeakAsync(text, cancellationToken);
}

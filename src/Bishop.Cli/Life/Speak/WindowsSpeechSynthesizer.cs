using System.Runtime.Versioning;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace Bishop.Cli.Life.Speak;

[SupportedOSPlatform("windows10.0.10240.0")]
internal static class WindowsSpeechSynthesizer
{
    public static async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        using var synth = new SpeechSynthesizer();
        using var stream = await synth.SynthesizeTextToStreamAsync(text).AsTask(cancellationToken);

        var size = (int)stream.Size;
        var buffer = new byte[size];
        using (var reader = new DataReader(stream))
        {
            await reader.LoadAsync((uint)size).AsTask(cancellationToken);
            reader.ReadBytes(buffer);
        }

        using var ms = new MemoryStream(buffer);
        using var player = new System.Media.SoundPlayer(ms);
        player.PlaySync();
    }
}

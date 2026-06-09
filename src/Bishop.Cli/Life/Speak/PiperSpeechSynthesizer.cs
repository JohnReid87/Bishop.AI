using System.Diagnostics;
using System.Runtime.Versioning;

namespace Bishop.Cli.Life.Speak;

[SupportedOSPlatform("windows")]
internal static class PiperSpeechSynthesizer
{
    public static bool IsConfigured =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BISHOP_PIPER_EXE")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BISHOP_PIPER_VOICE"));

    public static async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var exe = Environment.GetEnvironmentVariable("BISHOP_PIPER_EXE")
            ?? throw new InvalidOperationException("BISHOP_PIPER_EXE not set");
        var voice = Environment.GetEnvironmentVariable("BISHOP_PIPER_VOICE")
            ?? throw new InvalidOperationException("BISHOP_PIPER_VOICE not set");

        var wavPath = Path.Combine(Path.GetTempPath(), $"bishop-piper-{Guid.NewGuid():N}.wav");

        var psi = new ProcessStartInfo(exe)
        {
            ArgumentList = { "--model", voice, "--output_file", wavPath, "--quiet" },
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using (var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start piper"))
            {
                await proc.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
                proc.StandardInput.Close();

                var errTask = proc.StandardError.ReadToEndAsync(cancellationToken);
                await proc.WaitForExitAsync(cancellationToken);
                var err = await errTask;

                if (proc.ExitCode != 0)
                    throw new InvalidOperationException($"piper exited {proc.ExitCode}: {err}");
            }

            // Hand off to Life.App over the speak pipe when it's listening so
            // the dashboard's viz panel can render the utterance. Falls back
            // to local SoundPlayer when no listener is reachable, preserving
            // today's standup-without-viewer flow.
            var handedOff = await SpeakPipePublisher.TryPublishAsync(wavPath, cancellationToken);
            if (!handedOff)
            {
                using var player = new System.Media.SoundPlayer(wavPath);
                player.PlaySync();
            }
        }
        finally
        {
            try { File.Delete(wavPath); }
            catch (Exception ex) { Debug.WriteLine($"PiperSpeechSynthesizer: temp wav delete failed: {ex.Message}"); }
        }
    }
}

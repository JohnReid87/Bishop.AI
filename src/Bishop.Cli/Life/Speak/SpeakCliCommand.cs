using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.Versioning;

namespace Bishop.Cli.Life.Speak;

[SupportedOSPlatform("windows10.0.10240.0")]
internal sealed class SpeakCliCommand : Command
{
    public SpeakCliCommand() : base("speak", "Synthesize text to speech via WinRT and play synchronously to the default output device")
    {
        var textArg = new Argument<string?>("text", () => null, "Text to speak. If omitted, reads from stdin.");
        AddArgument(textArg);

        this.SetHandler(async (InvocationContext ctx) =>
        {
            var token = ctx.GetCancellationToken();
            var text = ctx.ParseResult.GetValueForArgument(textArg);

            if (string.IsNullOrEmpty(text))
                text = await Console.In.ReadToEndAsync(token);

            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                await WindowsSpeechSynthesizer.SpeakAsync(text, token);
            }
            catch (Exception ex)
            {
                // Silent degradation by default — but surface to stderr so a manual `bishop life speak`
                // invocation gives the user something to diagnose with.
                Console.Error.WriteLine($"bishop life speak: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
    }
}

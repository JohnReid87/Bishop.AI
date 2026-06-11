using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.Versioning;

namespace Bishop.Cli.Life.Speak;

[SupportedOSPlatform("windows10.0.10240.0")]
internal sealed class SpeakPreludeCliCommand : Command
{
    public SpeakPreludeCliCommand() : base("speak-prelude", "Speak a short randomly chosen acknowledgement to fill pre-context silence at stand-up launch")
    {
        this.SetHandler(async (InvocationContext ctx) =>
        {
            var token = ctx.GetCancellationToken();
            var phrase = SpeakPreludePhrases.Pick(Random.Shared);

            try
            {
                await Speaker.SpeakAsync(phrase, token);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"bishop life speak-prelude: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
    }
}

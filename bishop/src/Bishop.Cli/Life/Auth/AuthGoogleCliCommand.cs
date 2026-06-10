using Bishop.Life.Core;
using Bishop.Life.Core.Google;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.Versioning;

namespace Bishop.Cli.Life.Auth;

[SupportedOSPlatform("windows")]
internal sealed class AuthGoogleCliCommand : Command
{
    public AuthGoogleCliCommand() : base("google", "Authorize Bishop to read your primary Google Calendar (installed-app OAuth)")
    {
        this.SetHandler(async (InvocationContext ctx) =>
        {
            var token = ctx.GetCancellationToken();

            var settings = GoogleOAuthSettings.FromEnvironment();
            if (settings is null)
            {
                Console.Error.WriteLine(
                    $"error: set {GoogleOAuthSettings.ClientIdEnvVar} and {GoogleOAuthSettings.ClientSecretEnvVar} " +
                    "to your Google Cloud Console OAuth client (type \"Desktop app\") and retry.");
                ctx.ExitCode = 1;
                return;
            }

            var store = new GoogleTokenStore();
            var service = new GoogleCalendarService(settings, store);

            try
            {
                Console.WriteLine("Opening browser for Google consent — approve to continue...");
                await service.AuthorizeAsync(token);
                Console.WriteLine($"Authorized. Refresh token saved (DPAPI-encrypted) at {store.FilePath}.");
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("error: authorization cancelled.");
                ctx.ExitCode = 1;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });
    }
}

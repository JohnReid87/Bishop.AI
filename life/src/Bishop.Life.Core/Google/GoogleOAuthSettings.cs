namespace Bishop.Life.Core.Google;

/// <summary>
/// OAuth client credentials for the installed-app Google flow. Read from env vars at runtime so
/// the repo never carries a client_id/secret. The user creates a Google Cloud Console OAuth
/// client (type "Desktop app") once and sets these vars on their machine.
/// </summary>
public sealed record GoogleOAuthSettings(string ClientId, string ClientSecret)
{
    public const string ClientIdEnvVar = "BISHOP_GOOGLE_CLIENT_ID";
    public const string ClientSecretEnvVar = "BISHOP_GOOGLE_CLIENT_SECRET";

    /// <summary>
    /// Reads OAuth credentials from <see cref="ClientIdEnvVar"/> and <see cref="ClientSecretEnvVar"/>.
    /// Returns null if either is missing — callers should surface a setup message rather than throwing.
    /// </summary>
    public static GoogleOAuthSettings? FromEnvironment()
    {
        var clientId = Environment.GetEnvironmentVariable(ClientIdEnvVar);
        var clientSecret = Environment.GetEnvironmentVariable(ClientSecretEnvVar);
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return null;
        return new GoogleOAuthSettings(clientId, clientSecret);
    }
}

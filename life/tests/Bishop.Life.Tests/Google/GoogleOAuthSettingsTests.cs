using Bishop.Life.Core.Google;
using FluentAssertions;

namespace Bishop.Life.Tests.Google;

public class GoogleOAuthSettingsTests
{
    [Fact]
    public void FromEnvironment_BothVarsSet_ReturnsSettings()
    {
        using var _ = new EnvScope(GoogleOAuthSettings.ClientIdEnvVar, "test-client-id");
        using var __ = new EnvScope(GoogleOAuthSettings.ClientSecretEnvVar, "test-client-secret");

        var settings = GoogleOAuthSettings.FromEnvironment();

        settings.Should().NotBeNull();
        settings!.ClientId.Should().Be("test-client-id");
        settings.ClientSecret.Should().Be("test-client-secret");
    }

    [Fact]
    public void FromEnvironment_ClientIdMissing_ReturnsNull()
    {
        using var _ = new EnvScope(GoogleOAuthSettings.ClientIdEnvVar, null);
        using var __ = new EnvScope(GoogleOAuthSettings.ClientSecretEnvVar, "secret");

        GoogleOAuthSettings.FromEnvironment().Should().BeNull();
    }

    [Fact]
    public void FromEnvironment_ClientSecretMissing_ReturnsNull()
    {
        using var _ = new EnvScope(GoogleOAuthSettings.ClientIdEnvVar, "id");
        using var __ = new EnvScope(GoogleOAuthSettings.ClientSecretEnvVar, null);

        GoogleOAuthSettings.FromEnvironment().Should().BeNull();
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _prior;

        public EnvScope(string name, string? value)
        {
            _name = name;
            _prior = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _prior);
    }
}

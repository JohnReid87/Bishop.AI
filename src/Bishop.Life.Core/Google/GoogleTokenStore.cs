using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bishop.Life.Core.Google;

/// <summary>
/// Persists a Google OAuth refresh token to disk, encrypted under the current Windows user via DPAPI.
/// The token never appears in plaintext on disk; another Windows user on the same machine cannot
/// decrypt it. Uses the same <c>.tmp</c> + rename pattern as <see cref="LifePlanFileService"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GoogleTokenStore
{
    private readonly string _filePath;

    public GoogleTokenStore() : this(LifePlanPaths.ResolveGoogleTokenPath()) { }

    public GoogleTokenStore(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public bool Exists() => File.Exists(_filePath);

    public string? LoadRefreshToken()
    {
        if (!File.Exists(_filePath))
            return null;

        var json = File.ReadAllText(_filePath);
        var payload = JsonSerializer.Deserialize<TokenPayload>(json, s_jsonOpts);
        if (payload is null || string.IsNullOrEmpty(payload.ProtectedRefreshToken))
            return null;

        var protectedBytes = Convert.FromBase64String(payload.ProtectedRefreshToken);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public void SaveRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var plainBytes = Encoding.UTF8.GetBytes(refreshToken);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        var payload = new TokenPayload(Convert.ToBase64String(protectedBytes), DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(payload, s_jsonOpts);

        var tempPath = _filePath + LifePlanPaths.TempSuffix;
        File.WriteAllText(tempPath, json);

        if (File.Exists(_filePath))
            File.Replace(tempPath, _filePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, _filePath);
    }

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record TokenPayload(string ProtectedRefreshToken, DateTimeOffset SavedAt);
}

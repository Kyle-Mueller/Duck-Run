using System.Security.Cryptography;

namespace DuckRun.Dashboard.Authentication;

/// <summary>
/// Generates DSN public keys. 32-byte random values base64url-encoded (≈ 43 chars, URL-safe).
/// </summary>
internal static class ApiKeyGenerator
{
    public static string NewPublicKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

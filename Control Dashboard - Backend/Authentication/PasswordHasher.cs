using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace DuckRun.Dashboard.Authentication;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher. Output format: <c>v1.{iterations}.{salt-b64}.{hash-b64}</c>.
/// </summary>
internal static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 210_000;
    private const string Marker = "v1";

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var derived = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, Iterations, HashBytes);
        return $"{Marker}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(password)) return false;

        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != Marker) return false;
        if (!int.TryParse(parts[1], out var iters) || iters < 1) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iters, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

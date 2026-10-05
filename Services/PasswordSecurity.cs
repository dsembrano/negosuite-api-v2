using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public static class PasswordSecurity
{
    // Retain the persisted V1 format. A password-hash migration is a separate rollout.
    public static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    public static bool Matches(string input, string stored) => input != null && stored != null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(input)), Encoding.UTF8.GetBytes(stored));
    public static void ValidateNew(string password)
    {
        const string symbols = "!@#$%^&*()_+-=[]{};':\"\\|,.<>/?";
        Require(password != null && password.Length >= 8 && password.Length <= 250 && !password.Contains('\n') &&
            password.Any(c => c >= '0' && c <= '9') && password.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) &&
            password.Any(symbols.Contains), "Use at least 8 characters, including a letter, a number, and a symbol.");
    }
}

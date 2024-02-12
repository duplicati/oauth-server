using System.Security.Cryptography;
using System.Text;

namespace OAuthServer;

/// <summary>
/// Helper class that hashes to a base64 string
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Hashes a string in UTF-8 encoding and returns the result as a base64-encoded string
    /// </summary>
    /// <param name="input">The string to hash</param>
    /// <returns>The base64 encoded hash</returns>
    public static string HashToBase64String(this string input)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace OAuthServer;

/// <summary>
/// A password generator based on crypto strength PRNG
/// </summary>
public static class PasswordGenerator
{
    /// <summary>
    /// The shared PRNG used for creating passwords
    /// </summary>
    private static RandomNumberGenerator PRNG = RandomNumberGenerator.Create();

    /// <summary>
    /// The lock guarding access to the shared <see cref="PRNG"/>
    /// </summary>
    private static readonly object _lock = new object();

    /// <summary>
    /// The character classes used to select from
    /// </summary>
    private static readonly ImmutableArray<string> CharacterClasses = new[] {
        "abcdefghijklmnopqrstuvwxyz",
        "0123456789",
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        "!-_."
    }.ToImmutableArray();

    /// <summary>
    /// Generate a password using a crypto-strength PRNG
    /// </summary>
    /// <param name="length">The length of the password</param>
    /// <returns>The password</returns>
    public static string Generate(int length = 32)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 1");
        var sb = new StringBuilder();

        // Prevent the PRNG from being used with multiple threads,
        // but don't create a new PRNG for each password
        lock (_lock)
        {
            var buffer = new byte[4];
            var lastClass = RndNext(buffer, CharacterClasses.Length);
            while (sb.Length < length)
            {
                var thisClass = RndNext(buffer, CharacterClasses.Length);
                if (thisClass != lastClass)
                {
                    lastClass = thisClass;
                    var src = CharacterClasses[lastClass];
                    sb.Append(src[RndNext(buffer, src.Length)]);
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a new random integer, using the shared <see cref="PRNG"/> instance
    /// </summary>
    /// <param name="buffer">The buffer to fill; must be 4 bytes long</param>
    /// <param name="max">The maximum value to return</param>
    /// <returns>A positive integer between 0 (inclusive) and max (exclusive)</returns>
    private static int RndNext(byte[] buffer, int max)
    {
        PRNG.GetBytes(buffer, 0, 4);
        return (int)(BitConverter.ToUInt32(buffer) % max);
    }
}

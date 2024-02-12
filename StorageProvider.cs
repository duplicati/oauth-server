using System.Text;
using System.Text.Json;

namespace OAuthServer;

/// <summary>
/// Wrapper class that provides encrypted key storage
/// </summary>
public class StorageProvider
{
    [Serializable]
    public class DecryptingFailedException : Exception
    {
        public DecryptingFailedException() { }
        public DecryptingFailedException(string message) : base(message) { }
        public DecryptingFailedException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Reduce on-disk storage for encrypted entities
    /// </summary>
    static StorageProvider()
    {
        SharpAESCrypt.SharpAESCrypt.Extension_InsertPlaceholder = false;
    }

    /// <summary>
    /// The connection string for the storage destination
    /// </summary>
    private readonly string m_storageString;

    /// <summary>
    /// The minimum expiration for an access token (when nothing is reported from service)
    /// </summary>
    public static long MinimumExpirationLength = 1000;

    /// <summary>
    /// Returns the best-guess expiration time in seconds
    /// </summary>
    /// <param name="a">One expiration time</param>
    /// <param name="b">Another expiration time</param>
    /// <returns>The best-guess expiration time</returns>
    public static long ExpirationSeconds(long a, long b)
        => Math.Max(Math.Max(a, b), MinimumExpirationLength);

    /// <summary>
    /// Creates a new storage provider
    /// </summary>
    /// <param name="storageString">The storage destination</param>
    public StorageProvider(string storageString)
    {
        if (!Directory.Exists(storageString))
            Directory.CreateDirectory(storageString);
        m_storageString = storageString;
    }

    /// <summary>
    /// A parsed remote storage token
    /// </summary>
    /// <param name="ServiceId">The service the token was created for</param>
    /// <param name="Expires">The time when the access token expires</param>
    /// <param name="AccessToken">The access token</param>
    /// <param name="RefreshToken">The refresh token</param>
    /// <param name="Json">The complete original JSON response</param>
    public record StoredEntry(string ServiceId, DateTime Expires, string AccessToken, string RefreshToken, string Json);

    /// <summary>
    /// Internal record for de-serializing the OAuth JSON response
    /// </summary>
    /// <param name="access_token">The current access token</param>
    /// <param name="refresh_token">The refresh token</param>
    /// <param name="expires">The number of seconds before the access token expires</param>
    private record OAuthEntry(string access_token, string refresh_token, long expires, long expires_in);

    /// <summary>
    /// Creates a new auth-token
    /// </summary>
    /// <param name="serviceId">The service to create it for</param>
    /// <param name="json">The JSON to store</param>
    /// <param name="cancellationToken">The cancellation token to use</param>
    /// <returns>The auth-token</returns>
    public async Task<string> CreateAuthTokenAsync(string serviceId, string json, CancellationToken cancellationToken)
    {
        // Generate key and password
        string keyId = Guid.NewGuid().ToString("N");
        string password = PasswordGenerator.Generate();

        var resp = JsonSerializer.Deserialize<OAuthEntry>(json)
            ?? throw new InvalidDataException("Response JSON could not be deserialized");
        var expires = DateTime.UtcNow.AddSeconds(ExpirationSeconds(resp.expires, resp.expires_in));

        var entry = new StoredEntry(serviceId, expires, resp.access_token, resp.refresh_token, json);
        await EncryptAndWriteEntryAsync(keyId, password, entry, cancellationToken);
        return $"{keyId}:{password}";
    }

    /// <summary>
    /// Updates a stored entry
    /// </summary>
    /// <param name="keyId">The key to update</param>
    /// <param name="password">The password to use</param>
    /// <param name="json">The json response</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    public async Task UpdateEntryAsync(string keyId, string password, string json, CancellationToken cancellationToken)
    {
        var resp = JsonSerializer.Deserialize<OAuthEntry>(json)
            ?? throw new InvalidDataException("Response JSON could not be deserialized");

        var existing = await GetFromKeyIdAsync(keyId, password, cancellationToken);
        var expires = DateTime.UtcNow.AddSeconds(ExpirationSeconds(resp.expires, resp.expires_in));

        var updated = existing with
        {
            Json = json,
            Expires = expires,

            AccessToken = string.IsNullOrWhiteSpace(resp.access_token)
                ? existing.AccessToken
                : resp.access_token,

            RefreshToken = string.IsNullOrWhiteSpace(resp.refresh_token)
                ? existing.RefreshToken
                : resp.refresh_token
        };

        await EncryptAndWriteEntryAsync(keyId, password, updated, cancellationToken);
    }

    /// <summary>
    /// Retrieves a previously stored token
    /// </summary>
    /// <param name="keyId">The key ID</param>
    /// <param name="password">The password used to decrypt it</param>
    /// <param name="cancellationToken">The cancellation token to use</param>
    /// <returns>The decrypted instance</returns>
    public async Task<StoredEntry> GetFromKeyIdAsync(string keyId, string password, CancellationToken cancellationToken)
    {
        try
        {
            using var decrypted = new MemoryStream();

            using (var encrypted = await ReadEntryAsync(keyId, cancellationToken))
                SharpAESCrypt.SharpAESCrypt.Decrypt(password, encrypted, decrypted);

            decrypted.Position = 0;
            return JsonSerializer.Deserialize<StoredEntry>(decrypted)
                ?? throw new InvalidDataException("Failed to parse contents of decrypted file");
        }
        catch (Exception ex)
        {
            throw new DecryptingFailedException("Decryption failed, invalid key?", ex);
        }
    }

    /// <summary>
    /// Delete a previously stored token
    /// </summary>
    /// <param name="keyId">The key ID</param>
    /// <param name="cancellationToken">The cancellation token to use</param>
    /// <returns>An awaitable task</returns>
    public Task DeleteByKeyIdAsync(string keyId, CancellationToken cancellationToken)
        => DeleteEntryAsync(keyId, cancellationToken);

    /// <summary>
    /// Serializes the entry, encrypts it, and writes it to storage
    /// </summary>
    /// <param name="keyId">The key ID</param>
    /// <param name="password">The password to encrypt with</param>
    /// <param name="entry">The entry to encrypt</param>
    /// <param name="cancellationToken">The cancellation token to use</param>
    /// <returns>An awaitable task</returns>
    private async Task EncryptAndWriteEntryAsync(string keyId, string password, StoredEntry entry, CancellationToken cancellationToken)
    {
        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry));
        using (var enc = new MemoryStream())
        {
            using (var ms = new MemoryStream(plaintext))
                SharpAESCrypt.SharpAESCrypt.Encrypt(password, ms, enc);

            enc.Position = 0;
            await WriteEntryAsync(keyId, enc, cancellationToken);
        }
    }

    /// <summary>
    /// Writes an encrypted entry to persistent storage
    /// </summary>
    /// <param name="key">The key to use</param>
    /// <param name="data">The data to write</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    private async Task WriteEntryAsync(string key, Stream data, CancellationToken cancellationToken)
    {
        var path = Path.Combine(m_storageString, key);
        using (var fs = File.OpenWrite(path))
        {
            fs.SetLength(0);
            await data.CopyToAsync(fs, cancellationToken);
        }
    }

    /// <summary>
    /// Reads an encrypted entry from persistent storage
    /// </summary>
    /// <param name="key">The key to read</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    private Task<Stream> ReadEntryAsync(string key, CancellationToken cancellationToken)
    {
        var path = Path.Combine(m_storageString, key);
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    /// <summary>
    /// Deletes an entry from persistent storage
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An awaitable task</returns>
    private Task DeleteEntryAsync(string key, CancellationToken cancellationToken)
    {
        var path = Path.Combine(m_storageString, key);
        File.Delete(path);
        return Task.CompletedTask;
    }
}

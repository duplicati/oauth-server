using Microsoft.Extensions.Caching.Memory;

namespace OAuthServer;

/// <summary>
/// Non-persisted cache with expiration
/// </summary>
/// <typeparam name="T">The type of item being stored</typeparam>
public class MemCacher<T>
{
    private readonly MemoryCache m_cache = new MemoryCache(
        new MemoryCacheOptions() {
            //SizeLimit = 1024 * 32
        }
    );

    /// <summary>
    /// The standard duration of a key
    /// </summary>
    private readonly TimeSpan DEFAULT_DURATION;

    /// <summary>
    /// Creates the cacher instance
    /// </summary>
    /// <param name="default_duration">The default expiry time for items</param>
    public MemCacher(TimeSpan? default_duration = null)
    {
        DEFAULT_DURATION = default_duration ?? TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Returns the value or null
    /// </summary>
    /// <param name="key">The key to locate</param>
    /// <returns>The value or null</returns>
    public T? GetValue(string key)
        => m_cache.Get<T>(key);

    /// <summary>
    /// Sets the value in the cache
    /// </summary>
    /// <param name="value">The value to store</param>
    /// <param name="key">The key to store it under</param>
    /// <param name="duration">The time the entry is valid</param>
    public void SetValue(T value, string key, TimeSpan? duration = null)
    {
        m_cache.Set(key, value, duration ?? DEFAULT_DURATION);
    }
}
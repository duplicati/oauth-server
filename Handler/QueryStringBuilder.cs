namespace OAuthServer.Handler;

/// <summary>
/// Helper class for creating query strings
/// </summary>
public class QueryStringBuilder
{
    /// <summary>
    /// The internal list of keys and values
    /// </summary>
    private readonly List<(string Key, string? Value)> _values = new();

    /// <summary>
    /// Creates an empty query string container
    /// </summary>
    public QueryStringBuilder() { }

    /// <summary>
    /// Adds a key and value to the query string
    /// </summary>
    /// <param name="key">The key to use</param>
    /// <param name="value">The value to use</param>
    /// <returns>The querystring instance for chaining calls</returns>
    public QueryStringBuilder Add(string key, string? value)
    {
        _values.Add((key, value));
        return this;
    }

    /// <summary>
    /// Adds a set of values of to the query string builder
    /// </summary>
    /// <param name="values">The set of values to add</param>
    /// <returns>The querystring instance for chaining calls</returns>
    public QueryStringBuilder Add(params (string Key, string? Value)[] values)
    {
        _values.AddRange(values);
        return this;
    }

    /// <summary>
    /// Creates a stringified uri-component from the current list of keys and values
    /// </summary>
    /// <returns>The uri-component as a string</returns>
    public string ToUriComponent()
        => QueryString.Create(_values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).ToUriComponent();

    /// <summary>
    /// Builds a url from a base url and a set of values
    /// </summary>
    /// <param name="baseUrl">The base url to use</param>
    /// <param name="values">The query string values to add</param>
    /// <returns>The encoded url</returns>
    public static string Build(string baseUrl, params (string Key, string? Value)[] values)
        => baseUrl + Build(values);

    /// <summary>
    /// Builds a query string from values
    /// </summary>
    /// <param name="values">The values to encode</param>
    /// <returns>The encode query fragment</returns>
    public static string Build(params (string Key, string? Value)[] values)
        => QueryString.Create(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).ToUriComponent();
}

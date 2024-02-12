namespace OAuthServer.Handler;

/// <summary>
/// Implements a handler that queries the fetch token state, 
/// allowing a client to retrieve the token after completing the login
/// </summary>
public static class Fetch
{
    /// <summary>
    /// Handles a fetch request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static Task Handle(HttpContext context, ApplicationContext appContext)
    {
        var token = context.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
            return WriteWrappedJsonAsync(context, new { error = "Missing token" });

        var fetchToken = appContext.FetchTokens.GetValue(token);
        if (fetchToken == null)
            return WriteWrappedJsonAsync(context, new { error = "No such entry" });

        if (string.IsNullOrWhiteSpace(fetchToken.AuthId))
            return WriteWrappedJsonAsync(context, new { wait = "Not ready" });

        return WriteWrappedJsonAsync(context, new { authid = fetchToken.AuthId });
    }

    /// <summary>
    /// Helper method that wraps json in jsonp if required
    /// </summary>
    /// <param name="context">The context to use</param>
    /// <param name="data">The data to return</param>
    /// <returns>The data, either as JSON or wrapped JSONP as requested</returns>
    private static Task WriteWrappedJsonAsync(HttpContext context, object data)
    {
        var inner = System.Text.Json.JsonSerializer.Serialize(data);
        var cb = context.Request.Query["callback"].FirstOrDefault()
            ?? context.Request.Query["jsonp"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(cb))
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(inner);
        }
        else
        {
            context.Response.ContentType = "application/javascript";
            return context.Response.WriteAsync($"{cb}({inner})");
        }
    }
}

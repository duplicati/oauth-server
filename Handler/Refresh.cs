using System.Text.Json;

namespace OAuthServer.Handler;

/// <summary>
/// Handles a request for a refresh token
/// </summary>
public static class Refresh
{
    /// <summary>
    /// Handles a refresh request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static async Task Handle(HttpContext context, ApplicationContext appContext)
    {
        string? authId;
        if (context.Request.Method == "POST")
            authId = context.Request.Form["authid"].FirstOrDefault();
        else if (context.Request.Method == "GET")
            authId = context.Request.Query["authid"].FirstOrDefault();
        else
            throw new HttpRequestException("Invalid HTTP method", null, System.Net.HttpStatusCode.MethodNotAllowed);

        authId ??= context.Request.Headers["X-AuthID"].FirstOrDefault();

        if (string.IsNullOrEmpty(authId))
            throw new HttpRequestException("Missing AuthID");

        if (authId.StartsWith("v2:"))
            await HandleV2(context, appContext, authId);
        else
            await HandleV1(context, appContext, authId);
    }

    /// <summary>
    /// Handles refresh of V2 tokens
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <param name="authId">The auth-id string</param>
    /// <returns>An awaitable token</returns>
    private static async Task HandleV2(HttpContext context, ApplicationContext appContext, string authId)
    {
        var parts = authId.Split(':', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts[0] != "v2")
            throw new HttpRequestException("Invalid AuthId, must be v2 format");

        var serviceId = parts[1];
        var refreshToken = parts[2];
        var service = appContext.Services.GetValueOrDefault(serviceId);
        if (service == null)
            throw new HttpRequestException("Service not supported");

        if (refreshToken.Length < 6)
            throw new HttpRequestException("Invalid refresh token");

        var cacheKey = QueryStringBuilder.Build("/v2/token", ("id", refreshToken.HashToBase64String()), ("service", serviceId));
        if (await TryEmitCachedResponse(context, appContext, cacheKey))
            return;

        await RefreshAndReportResult(context, appContext, service, cacheKey, refreshToken);
    }

    /// <summary>
    /// Handles refresh of V1 tokens
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <param name="authId">The auth-id string</param>
    /// <returns>An awaitable token</returns>
    private static async Task HandleV1(HttpContext context, ApplicationContext appContext, string authId)
    {
        var parts = authId.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new HttpRequestException("Invalid AuthId format");

        if (appContext.Storage == null)
            throw new HttpRequestException("Persisted storage is not configured");

        var keyId = parts[0];
        var password = parts[1];

        var cacheKey = QueryStringBuilder.Build("/v1/token", ("password", password.HashToBase64String()), ("id", keyId));

        if (await TryEmitCachedResponse(context, appContext, cacheKey))
            return;

        StorageProvider.StoredEntry entry;
        try
        {
            entry = await appContext.Storage.GetFromKeyIdAsync(keyId, password, context.RequestAborted);
        }
        catch (StorageProvider.DecryptingFailedException dex)
        {
            context.Response.Headers.Append("X-Reason", "Invalid key or password");
            throw new HttpRequestException("Invalid key or password", dex, System.Net.HttpStatusCode.Unauthorized);
        }

        var service = appContext.Services.GetValueOrDefault(entry.ServiceId);
        if (service == null)
            throw new HttpRequestException("Service not supported");

        if (entry.RefreshToken.Length < 6)
            throw new HttpRequestException("Invalid refresh token");

        var oauthresp = await RefreshAndReportResult(context, appContext, service, cacheKey, entry.RefreshToken);
        if (oauthresp?.json != null)
            await appContext.Storage.UpdateEntryAsync(keyId, password, oauthresp.json, context.RequestAborted);
    }

    /// <summary>
    /// Attempts to emit a cached response, if a freshly issued access token is in memory
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <param name="cacheKey">The cache key to use</param>
    /// <returns><c>true</c> if a cached response was reported;<c>false</c> otherwise</returns>
    private static async Task<bool> TryEmitCachedResponse(HttpContext context, ApplicationContext appContext, string cacheKey)
    {
        var cachedToken = appContext.AccessTokens.GetValue(cacheKey);
        if (cachedToken != null)
        {
            var expires_secs = (cachedToken.Expires - DateTime.UtcNow).TotalSeconds;
            if (expires_secs > 30)
            {
                // Return cached token
                await context.Response.WriteAsJsonAsync(new AuthResponse(cachedToken.Token, (int)expires_secs, cachedToken.ServiceId));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Performs the refresh and sends the access token to the client, updating the cache as well
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <param name="service">The service to query</param>
    /// <param name="cacheKey">The caching key to update</param>
    /// <param name="refreshToken">The refresh token to use for obtaining the access token</param>
    /// <returns>The refresh response</returns>
    private static async Task<OAuthResponse> RefreshAndReportResult(HttpContext context, ApplicationContext appContext, ServiceConfiguration service, string cacheKey, string refreshToken)
    {
        var oauthresp = await SendRefreshRequest(appContext.HttpClient, service, refreshToken, context.RequestAborted);
        var validDuration = TimeSpan.FromSeconds(oauthresp.expires_in - 10);
        var expires = DateTime.UtcNow + validDuration;

        appContext.AccessTokens.SetValue(new AccessToken(oauthresp.access_token, expires, service.Id), cacheKey, validDuration);
        await context.Response.WriteAsJsonAsync(new AuthResponse(oauthresp.access_token, (int)validDuration.TotalSeconds, service.Id));
        return oauthresp;
    }

    /// <summary>
    /// Sends the refresh request to the remote OAuth provider and returns the response
    /// </summary>
    /// <param name="httpClient">The http client to use</param>
    /// <param name="service">The service to query</param>
    /// <param name="refreshToken">The refresh token to send</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The parsed response</returns>
    private static async Task<OAuthResponse> SendRefreshRequest(HttpClient httpClient, ServiceConfiguration service, string refreshToken, CancellationToken cancellationToken)
    {
        var req = new[] {
            ("client_id", service.ClientId),
            ("refresh_token", refreshToken),
            ("grant_type", "refresh_token")
        }.ToList();

        if (!string.IsNullOrWhiteSpace(service.ClientSecret))
            req.Add(("client_secret", service.ClientSecret));
        if (!service.NoRedirectUriForRefreshRequest)
            req.Add(("redirect_uri", service.RedirectUri));

        var content = new FormUrlEncodedContent(req.Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2)));
        using var data = await httpClient.PostAsync(service.AuthUrl, content, cancellationToken);
        var json = await data.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        data.EnsureSuccessStatusCode();

        var oauthresp = JsonSerializer.Deserialize<OAuthResponse>(json)
            ?? throw new Exception("Failed to read JSON response");

        if (string.IsNullOrWhiteSpace(oauthresp.access_token))
            throw new Exception("Unexpected JSON response with no token");

        // Patch in the original JSON
        return oauthresp with { json = json };
    }

    /// <summary>
    /// The response written to the client
    /// </summary>
    /// <param name="access_token">The generated access token</param>
    /// <param name="expires">The expiration seconds</param>
    /// <param name="type">The service type</param>
    private record AuthResponse(string access_token, int expires, string type);

    /// <summary>
    /// The access token from the OAuth server
    /// </summary>
    /// <param name="access_token">The access token</param>
    /// <param name="expires_in">The number of seconds the token is valid in</param>
    /// <param name="json">The raw JSON data</param>
    private record OAuthResponse(string access_token, int expires_in, string? json);
}

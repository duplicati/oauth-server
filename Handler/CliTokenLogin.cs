using System.Text.Json;

namespace OAuthServer.Handler;

/// <summary>
/// Handles CLI Token Login request for Jottacloud
/// </summary>
public static class CliTokenLogin
{
    /// <summary>
    /// The amount of time the token is stored in memory after being generated, if invoked with a fetch token
    /// </summary>
    private static readonly TimeSpan SuccessTokenLifetime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Handles a cli token login request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static async Task Handle(HttpContext context, ApplicationContext appContext)
    {
        var serviceId = context.Request.Form["id"].FirstOrDefault() ?? string.Empty;
        var token = context.Request.Form["token"].FirstOrDefault();
        var fetchTokenKey = context.Request.Form["fetchtoken"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token) || token.Length < 6)
            throw new HttpRequestException("Invalid token");

        var service = appContext.Services.GetValueOrDefault(serviceId);
        if (service == null || !service.CliToken)
            throw new HttpRequestException("Service not supported");

        // Convert from base64 url-safe to regular base64 with padding
        var base64token = token.Replace('-', '+').Replace('_', '/');
        if (base64token.Length % 4 == 3)
            base64token += "=";
        else if (base64token.Length % 4 == 2)
            base64token += "==";

        JsonEntry jsonEntry;
        try
        {
            jsonEntry = JsonSerializer.Deserialize<JsonEntry>(Convert.FromBase64String(base64token))
                ?? throw new Exception("Failed to deserialize Json");
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("Invalid token data", ex);
        }

        var req = new[] {
            ("client_id", service.ClientId),
            ("grant_type", "password"),
            ("scope", service.Scope),
            ("username", jsonEntry.username),
            ("password", jsonEntry.auth_token)
        }.ToList();

        var content = new FormUrlEncodedContent(req.Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2)));
        using var data = await appContext.HttpClient.PostAsync(service.AuthUrl, content, context.RequestAborted);
        data.EnsureSuccessStatusCode();

        var resp = await data.Content.ReadFromJsonAsync<OAuthResponse>(cancellationToken: context.RequestAborted)
            ?? throw new Exception("Failed to deserialize JSON response");

        var authid = $"v2:{service.Id}:{resp.access_token}";
        FetchToken? fetchToken;
        if (!string.IsNullOrWhiteSpace(fetchTokenKey) && (fetchToken = appContext.FetchTokens.GetValue(fetchTokenKey)) != null)
            appContext.FetchTokens.SetValue(new FetchToken(authid, null), fetchTokenKey, SuccessTokenLifetime);

        await context.Response.WriteAsync(
            appContext.Render.LoggedIn(new TemplateRenderers.LoggedInRenderArgs(
                Service: service.Name,
                AuthId: authid,
                DeAuthLink: null,
                AdditionalData: []
            ))
        );
    }

    /// <summary>
    /// Persisted entry in JSON
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="auth_token">The auth token</param>
    private sealed record JsonEntry(string username, string auth_token);

    /// <summary>
    /// The access token from the OAuth server
    /// </summary>
    /// <param name="access_token">The access token</param>
    /// <param name="expires_in">The number of seconds the token is valid in</param>
    private record OAuthResponse(string access_token, int expires_in);

}

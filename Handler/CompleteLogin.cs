using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OAuthServer.Handler;

/// <summary>
/// Handles the login callback from the OAuth server.
/// This is called after the user grants access on the remote server
/// After grabbing the refresh token, the logged-in.html page is
/// rendered
/// </summary>
public static class CompleteLogin
{
    /// <summary>
    /// The amount of time the token is stored in memory after being generated, if invoked with a fetch token
    /// </summary>
    private static readonly TimeSpan SuccessTokenLifetime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Handles a login completion request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static async Task Handle(HttpContext context, ApplicationContext appContext)
    {
        // Grab the assigned state and generated code
        var stateKey = context.Request.Query["state"].FirstOrDefault();
        var code = context.Request.Query["code"].FirstOrDefault();
        var token = context.Request.Query["token"].FirstOrDefault();

        if (string.IsNullOrEmpty(stateKey) || string.IsNullOrEmpty(code))
            throw new Exception("Missing state or code in callback");

        var state = appContext.ActiveRequests.GetValue(stateKey);
        if (state == null)
            throw new Exception("State is expired or invalid");

        var service = appContext.Services.GetValueOrDefault(state.ServiceId);
        if (service == null)
            throw new Exception("Service Id was invalid");

        var additionalData = new Dictionary<string, string>();
        if (service.AdditionalElements != null)
            foreach (var element in service.AdditionalElements.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var v = context.Request.Query[element].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(v))
                    additionalData[element] = v;
            }

        var redirect_uri = service.RedirectUri;
        if (!string.IsNullOrWhiteSpace(token))
            redirect_uri = QueryHelpers.AddQueryString(redirect_uri, "token", token);

        var req = new[] {
            ("client_id", service.ClientId),
            ("redirect_uri", redirect_uri),
            ("client_secret", service.ClientSecret),
            ("code", code),
            // ("state", stateKey),
            ("grant_type", "authorization_code")
        }.ToList();

        if (service.NoStateForTokenRequest)
            req.RemoveAll(x => x.Item1 == "state");

        // Special handling for pCloud, use alternate hostname for auth call (region specific)
        var authurl = service.AuthUrl;
        if (service.UseHostnameFromCallback)
        {
            var althostname = context.Request.Query["hostname"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(althostname))
                authurl = new UriBuilder(authurl) { Host = althostname }.Uri.ToString();
        }

        var content = new FormUrlEncodedContent(req.Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2)));
        using var data = await appContext.HttpClient.PostAsync(authurl, content, context.RequestAborted);
        var json = await data.Content.ReadAsStringAsync(context.RequestAborted);
        data.EnsureSuccessStatusCode();

        if (service.AccessTokenOnly)
        {
            var accResp = JsonSerializer.Deserialize<OAuthAccessTokenOnlyResponse>(json);
            var access_token = accResp?.access_token;
            var deauth_link = (string?)null;

            if (string.IsNullOrEmpty(access_token))
            {
                access_token = $"Server error, you must de-authorize {appContext.Configuration.AppName}";
                deauth_link = service.DeAuthLink;
            }

            await context.Response.WriteAsync(
                appContext.Render.LoggedIn(new TemplateRenderers.LoggedInRenderArgs(
                    Service: service.Name,
                    AuthId: access_token,
                    DeAuthLink: deauth_link,
                    AdditionalData: additionalData
                ))
            );

            return;
        }

        var resp = JsonSerializer.Deserialize<OAuthResponse>(json)
            ?? throw new Exception("Failed to deserialize JSON response");

        if (string.IsNullOrEmpty(resp.refresh_token))
        {
            await context.Response.WriteAsync(
                appContext.Render.LoggedIn(new TemplateRenderers.LoggedInRenderArgs(
                    Service: service.Name,
                    AuthId: $"Server error, you must de-authorize {appContext.Configuration.AppName}",
                    DeAuthLink: service.DeAuthLink,
                    AdditionalData: additionalData
                ))
            );

            return;
        }

        var authid = state.UseV2 || appContext.Storage == null
            ? $"v2:{service.Id}:{resp.refresh_token}"
            : await appContext.Storage.CreateAuthTokenAsync(service.Id, json, context.RequestAborted);

        FetchToken? fetchToken;
        if (!string.IsNullOrWhiteSpace(state.Token) && (fetchToken = appContext.FetchTokens.GetValue(state.Token)) != null)
            appContext.FetchTokens.SetValue(new FetchToken(authid, null), state.Token, SuccessTokenLifetime);

        await context.Response.WriteAsync(
            appContext.Render.LoggedIn(new TemplateRenderers.LoggedInRenderArgs(
                Service: service.Name,
                AuthId: authid,
                DeAuthLink: null,
                AdditionalData: additionalData
            ))
        );
    }

    /// <summary>
    /// The response received from the OAuth server
    /// </summary>
    /// <param name="refresh_token">The refresh token</param>
    private sealed record OAuthResponse(string? refresh_token);

    /// <summary>
    /// The response received from the OAuth server when only an access token is returned
    /// </summary>
    /// <param name="access_token">The access token</param>
    private sealed record OAuthAccessTokenOnlyResponse(string? access_token);
}

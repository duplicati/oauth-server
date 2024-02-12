namespace OAuthServer.Handler;

/// <summary>
/// Creates a state and redirects the user to the login page
/// </summary>
public static class StartLogin
{
    /// <summary>
    /// The lifetime of the state token
    /// </summary>
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Handles the request
    /// </summary>
    /// <param name="context">The request context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>A completed task</returns>
    public static Task Handle(HttpContext context, ApplicationContext appContext)
    {
        // Find the OAuth service we are authenticating against
        var id = context.Request.Query["id"].FirstOrDefault();
        var service = appContext.Services.GetValueOrDefault(id ?? string.Empty);
        if (service == null)
            throw new Exception($"No service found for: {id}");

        // Pass the fetch token if we are set up
        var token = context.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token) || appContext.FetchTokens.GetValue(token) == null)
            token = null;

        // We should use V2, unless storage is configured
        var useV2 = appContext.Storage == null || service.PreferV2;

        // Set up a state to provide access to the OAuth service
        var state = new RequestState(service.Id, token, useV2);
        var statekey = Guid.NewGuid().ToString("N");

        // Just in case....
        if (appContext.ActiveRequests.GetValue(statekey) != null)
            throw new Exception("Unexpected state key conflict");

        appContext.ActiveRequests.SetValue(state, statekey, StateLifetime);

        // Create the redirect url
        var link = QueryStringBuilder.Build(
            service.LoginUrl,

            ("client_id", service.ClientId),
            ("response_type", "code"),
            ("scope", service.Scope),
            ("state", statekey),
            ("redirect_uri", service.RedirectUri)
        );

        if (!string.IsNullOrWhiteSpace(service.ExtraUrl))
            link += service.ExtraUrl;

        context.Response.Redirect(link, false);
        return Task.CompletedTask;
    }
}
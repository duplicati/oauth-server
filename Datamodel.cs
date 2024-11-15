namespace OAuthServer;

/// <summary>
/// The complete application context
/// </summary>
/// <param name="Configuration">The application configuration</param>
/// <param name="Services">The configured and enabled services</param>
/// <param name="HttpClient">The shared <see cref="HttpClient"/></param>
/// <param name="ActiveRequests">The active requests cache</param>
/// <param name="FetchTokens">The active fetch tokens cache</param>
/// <param name="AccessTokens">The current access tokens cache</param>
/// <param name="Render">The render helper</param>
public sealed record ApplicationContext(
    ApplicationConfiguration Configuration,
    IReadOnlyDictionary<string, ServiceConfiguration> Services,
    HttpClient HttpClient,
    MemCacher<RequestState> ActiveRequests,
    MemCacher<FetchToken> FetchTokens,
    MemCacher<AccessToken> AccessTokens,
    TemplateRenderers Render,
    StorageProvider? Storage
);

/// <summary>
/// Record for keeping state during the login sequence
/// </summary>
/// <param name="ServiceId">The service id</param>
/// <param name="Token">The optional fetch token</param>
/// <param name="UseV2">Flag indicating if the token should be a V2 token with no storage</param>
public sealed record RequestState(
    string ServiceId,
    string? Token,
    bool UseV2
);

/// <summary>
/// The active fetch token
/// </summary>
/// <param name="AuthId">The resulting auth-id</param>
/// <param name="ErrorMessage">Any error message associated with the token</param>
public sealed record FetchToken(
    string? AuthId,
    string? ErrorMessage
);

/// <summary>
/// An active access token
/// </summary>
/// <param name="Token">The access token issued</param>
/// <param name="Expires">The token expiry</param>
/// <param name="ServiceId">The service the token is for</param>
public sealed record AccessToken(
    string Token,
    DateTime Expires,
    string ServiceId
);

/// <summary>
/// The typed HTML renderers
/// </summary>
/// <param name="Index">Render for the index page</param>
/// <param name="Index">Render for the logged-in page</param>
/// <param name="CliToken">Render for the CLI token page</param>
/// <param name="PrivacyPolicy">The privacy policy, pre-rendered</param>
/// <param name="Revoke">The revoke start part, pre-rendered</param>
/// <param name="PrivacyPolicy">The revoked result page</param>
public sealed record TemplateRenderers(
    Func<TemplateRenderers.IndexTemplateRenderArgs, string> Index,
    Func<TemplateRenderers.LoggedInRenderArgs, string> LoggedIn,
    Func<TemplateRenderers.CliTokenTemplateRenderArgs, string> CliToken,
    string PrivacyPolicy,
    string Revoke,
    Func<TemplateRenderers.RevokedTemplateRenderArgs, string> Revoked
)
{
    /// <summary>
    /// Render a service item on the index page
    /// </summary>
    /// <param name="Id">The service id</param>
    /// <param name="Display">The display name</param>
    /// <param name="Authlink">The link to start the auth process</param>
    /// <param name="Servicelink">The link to the service configuration</param>
    /// <param name="Brandimage">The brand image</param>
    /// <param name="Notes">Any notes</param>
    public sealed record RenderedServiceItem(
        string Id,
        string Display,
        string Authlink,
        string? Servicelink,
        string? Brandimage,
        string? Notes);

    /// <summary>
    /// Renders the index page
    /// </summary>
    /// <param name="RedirectId">The redirect id</param>
    /// <param name="Providers">The list of providers</param>
    public sealed record IndexTemplateRenderArgs(
        string? RedirectId,
        IReadOnlyList<RenderedServiceItem> Providers
    );

    /// <summary>
    /// Renders the logged-in page
    /// </summary>
    /// <param name="Service">The service name</param>
    /// <param name="AuthId">The auth id</param>
    /// <param name="DeAuthLink">The de-auth link</param>
    /// <param name="AdditionalData">Any additional data</param>
    public sealed record LoggedInRenderArgs(
        string Service,
        string AuthId,
        string? DeAuthLink,
        Dictionary<string, string> AdditionalData
    );

    /// <summary>
    /// Renders the CLI token page
    /// </summary>
    /// <param name="Id">The service id</param>
    /// <param name="Service">The service name</param>
    /// <param name="FetchToken">The fetch token</param>
    public sealed record CliTokenTemplateRenderArgs(
        string Id,
        string Service,
        string? FetchToken
    );

    /// <summary>
    /// Renders the revoked page
    /// </summary>
    /// <param name="Result">The result message</param>
    public sealed record RevokedTemplateRenderArgs(
        string Result
    );
}

/// <summary>
/// The application configuration setup
/// </summary>
/// <param name="Hostname">The hostname that is served from</param>
/// <param name="AppName">The app name to show</param>
/// <param name="DisplayName">The display name to show</param>
/// <param name="EnabledServiceIds">List of enabled service Ids</param>
/// <param name="SecretsFilePath">Path to the OAuth secrets</param>
/// <param name="SecretsPassphrase">Passhrase for decypting the OAuth secrets</param>
/// <param name="ConfigFilePath">Path to service config overrides</param>
/// <param name="StorageString">Path to a storage destination for v1 tokens</param>
/// <param name="CustomPrivacyPolicyUrl">Url for a custom privacy policy</param>
/// <param name="SeqLogUrl">Url for Seq log destination</param>
/// <param name="SeqLogApiKey">Optional API key for logging to Seq</param>
public sealed record ApplicationConfiguration(
    string Hostname,
    string AppName,
    string DisplayName,
    string EnabledServiceIds,
    string SecretsFilePath,
    string SecretsPassphrase,
    string ConfigFilePath,
    string StorageString,
    string CustomPrivacyPolicyUrl,
    string SeqLogUrl,
    string SeqLogApiKey
);

/// <summary>
/// Default settings for a service configuration
/// </summary>
/// <param name="Id">The service Id</param>
/// <param name="Name">The name of the service</param>
/// <param name="AuthUrl">The remote auth url</param>
/// <param name="LoginUrl">The remote login url</param>
/// <param name="Scope">The scope to request</param>
/// <param name="RedirectUri">The redirect uri to use</param>
/// <param name="ExtraUrl">Any extra url data to append</param>
/// <param name="ServiceLink">A link to the providers configuration page</param>
/// <param name="DeAuthLink">A link to the providers de-authorize page</param>
/// <param name="BrandImage">An image required for the provider</param>
/// <param name="Notes">Any notes for the provider</param>
/// <param name="Hidden">Hide the service on lists</param>
/// <param name="NoStateForTokenRequest">The token request does not accept a state</param>
/// <param name="NoRedirectUriForRefreshRequest">The provider does not accept a redirect uri for refresh requests</param>
/// <param name="CliToken">The request is for a CLI token</param>
/// <param name="PreferV2">The provider is preferable using v2 tokens</param>
/// <param name="AccessTokenOnly">The provider only returns an access token</param>
/// <param name="AdditionalElements">Any additional data reported from the provider</param>
public sealed record ServiceDefault(
    string Id,
    string Name = "",
    string AuthUrl = "",
    string LoginUrl = "",
    string Scope = "",
    string RedirectUri = ConfigurationLoader.DefaultCallbackUri,
    string? ExtraUrl = null,
    string? ServiceLink = null,
    string? DeAuthLink = null,
    string? BrandImage = null,
    string? Notes = null,
    bool Hidden = false,
    bool NoStateForTokenRequest = false,
    bool NoRedirectUriForRefreshRequest = false,
    bool CliToken = false,
    bool PreferV2 = false,
    bool AccessTokenOnly = false,
    bool UseHostnameFromCallback = false,
    string? AdditionalElements = null
);

/// <summary>
/// The settings for a service configuration
/// </summary>
/// <param name="Id">The service Id</param>
/// <param name="Name">The name of the service</param>
/// <param name="ClientId">The clientId for the service</param>
/// <param name="ClientSecret">The client secret for the service</param>
/// <param name="AuthUrl">The remote auth url</param>
/// <param name="LoginUrl">The remote login url</param>
/// <param name="Scope">The scope to request</param>
/// <param name="RedirectUri">The redirect uri to use</param>
/// <param name="ExtraUrl">Any extra urk data to append</param>
/// <param name="ServiceLink">A link to the providers configuration page</param>
/// <param name="DeAuthLink">A link to the providers de-authorize page</param>
/// <param name="BrandImage">An image required for the provider</param>
/// <param name="Notes">Any notes for the provider</param>
/// <param name="Hidden">Hide the service on lists</param>
/// <param name="NoStateForTokenRequest">The token request does not accept a state</param>
/// <param name="NoRedirectUriForRefreshRequest">The provider does not accept a redirect uri for refresh requests</param>
/// <param name="CliToken">The request is for a CLI token</param>
/// <param name="PreferV2">The provider is preferable using v2 tokens</param>
/// <param name="AccessTokenOnly">The provider only returns an access token</param>
/// <param name="UseHostnameFromCallback">Use the hostname from the callback in the AuthUrl</param>
/// <param name="AdditionalElements">Any additional data reported from the provider</param>
public sealed record ServiceConfiguration(
    string Id,
    string Name,
    string ClientId,
    string ClientSecret,
    string AuthUrl,
    string LoginUrl,
    string Scope,
    string RedirectUri,
    string? ExtraUrl,
    string? ServiceLink,
    string? DeAuthLink,
    string? BrandImage,
    string? Notes,
    bool Hidden,
    bool NoStateForTokenRequest,
    bool NoRedirectUriForRefreshRequest,
    bool CliToken,
    bool PreferV2,
    bool AccessTokenOnly,
    bool UseHostnameFromCallback,
    string? AdditionalElements
);
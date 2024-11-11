namespace OAuthServer;

/// <summary>
/// Configuration settings from the providers API descriptions.
/// These are not expected to be changed, but can be overridden via config.json
/// </summary>
public static class DefaultConfigurations
{
    /// <summary>
    /// The setup for Windows Live endpoint
    /// </summary>
    /// <remarks>Managed from: https://portal.azure.com</remarks>
    private static readonly ServiceDefault WindowsLive = new ServiceDefault(
        "WL",
        Name: "Microsoft OneDrive (Live Connect API)",
        AuthUrl: "https://login.live.com/oauth20_token.srf",
        LoginUrl: "https://login.live.com/oauth20_authorize.srf",
        Scope: "wl.offline_access wl.skydrive_update wl.skydrive",
        ServiceLink: "https://onedrive.live.com",
        Notes: "<p style=\"font-size: small\">By using the OAuth login service for OneDrive you agree to the <a href=\"https://www.microsoft.com/en-us/servicesagreement\" target=\"_blank\">Microsoft Service Agreement</a> and <a href=\"https://privacy.microsoft.com/en-us/privacystatement\" target=\"_blank\">Microsoft Online Privacy Statement</a></p>"
    );

    /// <summary>
    /// The setup for MS Graph API
    /// </summary>
    /// <remarks>Managed from: https://portal.azure.com</remarks>
    private static readonly ServiceDefault MicrosoftGraph = new ServiceDefault(
        "MSGRAPH",
        Name: "Microsoft OneDrive (Microsoft Graph API)",
        AuthUrl: "https://login.microsoftonline.com/common/oauth2/v2.0/token",
        LoginUrl: "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
        Scope: "offline_access Files.ReadWrite",
        ServiceLink: "https://onedrive.live.com"
    );

    /// <summary>
    /// The setup for Google Drive API
    /// </summary>
    /// <remarks>Managed from: https://console.developers.google.com</remarks>
    private static readonly ServiceDefault GoogleDrive = new ServiceDefault(
        "GD",
        Name: "Google Drive (limited)",
        AuthUrl: "https://www.googleapis.com/oauth2/v3/token",
        LoginUrl: "https://accounts.google.com/o/oauth2/auth",
        Scope: "https://www.googleapis.com/auth/drive.file",
        ExtraUrl: "&access_type=offline&approval_prompt=force",
        ServiceLink: "https://drive.google.com",
        DeAuthLink: "https://security.google.com/settings/security/permissions",
        BrandImage: "/google-btn.png"
    );

    /// <summary>
    /// The setup for Google Cloud Services API
    /// </summary>
    /// <remarks>Managed from: https://console.developers.google.com</remarks>
    private static readonly ServiceDefault GoogleCloudStorage = new ServiceDefault(
        "GCS",
        Name: "Google Cloud Storage",
        AuthUrl: "https://www.googleapis.com/oauth2/v3/token",
        LoginUrl: "https://accounts.google.com/o/oauth2/auth",
        Scope: "https://www.googleapis.com/auth/devstorage.read_write",
        ExtraUrl: "&access_type=offline&approval_prompt=force",
        ServiceLink: "https://cloud.google.com/storage/",
        DeAuthLink: "https://security.google.com/settings/security/permissions",
        BrandImage: "/google-btn.png"
    );

    /// <summary>
    /// The setup for box.com API
    /// </summary>
    /// <remarks>Managed from: https://app.box.com/developers/console</remarks>
    private static readonly ServiceDefault BoxCom = new ServiceDefault(
        "BOX",
        Name: "Box.com",
        AuthUrl: "https://api.box.com/oauth2/token",
        LoginUrl: "https://app.box.com/api/oauth2/authorize",
        Scope: "root_readwrite",
        ServiceLink: "https://www.box.com/pricing/personal/"
    );

    /// <summary>
    /// The setup for the Dropbox API
    /// </summary>
    /// <remarks>Managed from: https://www.dropbox.com/developers/apps</remarks>
    private static readonly ServiceDefault Dropbox = new ServiceDefault(
        "DROPBOX",
        Name: "Dropbox",
        AuthUrl: "https://api.dropboxapi.com/oauth2/token",
        LoginUrl: "https://www.dropbox.com/oauth2/authorize",
        Scope: "files.content.write files.content.read files.metadata.read files.metadata.write",
        ExtraUrl: "&token_access_type=offline",
        ServiceLink: "https://dropbox.com",
        NoStateForTokenRequest: true,
        NoRedirectUriForRefreshRequest: true
    );

    /// <summary>
    /// The setup for Jottacloud API
    /// </summary>
    /// <remarks>https://www.jottacloud.com/web/account</remarks>
    private static readonly ServiceDefault Jottacloud = new ServiceDefault(
        "JOTTA",
        Name: "Jottacloud",
        CliToken: true,
        Scope: "openid offline_access",
        ServiceLink: "https://jottacloud.com"
    );

    /// <summary>
    /// The setup for pCloud API
    /// </summary>
    /// <remarks>Managed from: https://docs.pcloud.com/my_apps/</remarks>
    private static readonly ServiceDefault pCloud = new ServiceDefault(
        "PCLOUD",
        Name: "pCloud",
        AuthUrl: "https://api.pcloud.com/oauth2_token",
        LoginUrl: "https://my.pcloud.com/oauth2/authorize",
        // Scope: "root_readwrite",
        ServiceLink: "https://pcloud.com/",
        AccessTokenOnly: true,
        UseHostnameFromCallback: true,
        AdditionalElements: "locationid,hostname"
    );


    /// <summary>
    /// Loads all known service defauls via reflection
    /// </summary>
    /// <returns>A lookup table with known service configuration defaults</returns>
    public static IReadOnlyDictionary<string, ServiceDefault> AllServices
        = typeof(DefaultConfigurations)
            .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Where(x => x.FieldType == typeof(ServiceDefault))
            .Select(x => x.GetValue(null))
            .OfType<ServiceDefault>()
            .ToDictionary(x => x.Id);

}
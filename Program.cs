using Microsoft.Extensions.FileProviders;
using OAuthServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var stateTokenCache = new MemCacher<RequestState>();
var fetchTokenCache = new MemCacher<FetchToken>();
var accessTokenCache = new MemCacher<AccessToken>();
var httpClient = new HttpClient(
    // Pr docs, we refresh every 15 min to ensure DNS TTL is re-applied
    new SocketsHttpHandler() { PooledConnectionLifetime = TimeSpan.FromMinutes(15) }
);

var appconfig = ConfigurationLoader.LoadApplicationConfiguration();
var renderers = ConfigurationLoader.LoadRenderers(appconfig);
var services = ConfigurationLoader.LoadServices(appconfig)
    .ToDictionary(x => x.Id);

var storage = string.IsNullOrWhiteSpace(appconfig.StorageString)
    ? null
    : new StorageProvider(appconfig.StorageString);

var appContext = new ApplicationContext(
    appconfig,
    services,
    httpClient,
    stateTokenCache,
    fetchTokenCache,
    accessTokenCache,
    renderers,
    storage
);

// Support LetsEncrypt
var le_path = Path.Combine(Directory.GetCurrentDirectory(), @".well-known");
if (Directory.Exists(le_path))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(le_path),
        RequestPath = new PathString("/.well-known"),
        ServeUnknownFileTypes = true // serve extensionless file
    });
}

/*
    // TODO: Implement this if it is still being used
    ('/token-state', TokenStateHandler),
*/

app.MapGet("/login", ctx => OAuthServer.Handler.StartLogin.Handle(ctx, appContext));
app.MapGet("/logged-in", ctx => OAuthServer.Handler.CompleteLogin.Handle(ctx, appContext));
app.MapGet("/privacy-policy", ctx => OAuthServer.Handler.PrivacyPolicy.Handle(ctx, appContext));
app.MapGet("/fetch", ctx => OAuthServer.Handler.Fetch.Handle(ctx, appContext));
app.MapGet("/cli-token", ctx => OAuthServer.Handler.CliToken.Handle(ctx, appContext));
app.MapPost("/cli-token-login", ctx => OAuthServer.Handler.CliTokenLogin.Handle(ctx, appContext));
app.MapGet("/refresh", ctx => OAuthServer.Handler.Refresh.Handle(ctx, appContext));
app.MapPost("/refresh", ctx => OAuthServer.Handler.Refresh.Handle(ctx, appContext));
app.MapGet("/revoke", ctx => OAuthServer.Handler.StartRevoke.Handle(ctx, appContext));
app.MapPost("/revoked", ctx => OAuthServer.Handler.CompleteRevoke.Handle(ctx, appContext));
app.MapGet("/", ctx => OAuthServer.Handler.Index.Handle(ctx, appContext));
        
app.Run();

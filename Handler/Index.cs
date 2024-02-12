using System.Net;

namespace OAuthServer.Handler;

/// <summary>
/// The index handler, responsible for starting the fetch request,
/// and rendering the initial page
/// </summary>
public static class Index
{
    private static TimeSpan FetchTokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Handles an index request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static async Task Handle(HttpContext context, ApplicationContext appContext)
    {
        // If there is a token associated with the request, the caller needs to track this token
        var fetchTokenKey = context.Request.Query["token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fetchTokenKey) && fetchTokenKey.Length > 8)
            appContext.FetchTokens.SetValue(new FetchToken(null, null), fetchTokenKey, FetchTokenLifetime);

        // If there is a type in the request, filter the services to only show this one item
        var filter = context.Request.Query["type"].FirstOrDefault();

        var serviceItems = appContext.Services.Values
            .Where(x => string.IsNullOrWhiteSpace(filter) || string.Equals(filter, x.Id))
            .Where(x => !string.IsNullOrWhiteSpace(filter) || !x.Hidden)
            .Select(x => new TemplateRenderers.RenderedServiceItem(
                x.Id,
                x.Name,
                QueryStringBuilder.Build(
                    x.CliToken ? "/cli-token" : "/login",
                    ("id", x.Id),
                    ("token", fetchTokenKey)
                ),
                x.ServiceLink,
                x.BrandImage,
                x.Notes
            ));

        context.Response.ContentType = "text/html";
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync(
            appContext.Render.Index(new TemplateRenderers.IndexTemplateRenderArgs(
                context.Request.Query["redir"].FirstOrDefault(),
                serviceItems.ToList()
            ))
        );
    }
}
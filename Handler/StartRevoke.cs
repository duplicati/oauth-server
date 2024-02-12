using System.Net;

namespace OAuthServer.Handler;

/// <summary>
/// Renders the revoke.html page
/// </summary>
public static class StartRevoke
{
    /// <summary>
    /// Handles a revoke request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static Task Handle(HttpContext context, ApplicationContext appContext)
    {
        context.Response.ContentType = "text/html";
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        return context.Response.WriteAsync(
            appContext.Render.Revoke
        );
    }
}

using System.Net;

namespace OAuthServer.Handler;

/// <summary>
/// Simple static handler that displays the pre-rendered privacy policy page
/// </summary>
public static class PrivacyPolicy
{
    /// <summary>
    /// Handles a privacy policy request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static Task Handle(HttpContext context, ApplicationContext appContext)
    {
        if (string.IsNullOrWhiteSpace(appContext.Configuration.CustomPrivacyPolicyUrl))
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            return context.Response.WriteAsync(
                appContext.Render.PrivacyPolicy
            );
        }
        else
        {
            context.Response.Redirect(appContext.Configuration.CustomPrivacyPolicyUrl);
            return Task.CompletedTask;
        }
    }
}
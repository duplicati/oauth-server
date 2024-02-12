using System.Net;

namespace OAuthServer.Handler;

/// <summary>
/// Handler for the cli-token input page
/// </summary>
public static class CliToken
{
    /// <summary>
    /// Handles a privacy policy request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static Task Handle(HttpContext context, ApplicationContext appContext)
    {
        var fetchTokenKey = context.Request.Query["token"].FirstOrDefault();
        var id = context.Request.Query["id"].FirstOrDefault() ?? string.Empty;
        var service = appContext.Services.GetValueOrDefault(id);

        if (service == null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return context.Response.WriteAsync("Invalid service id");
        }

        context.Response.ContentType = "text/html";
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        return context.Response.WriteAsync(
            appContext.Render.CliToken(new TemplateRenderers.CliTokenTemplateRenderArgs(
                Id: service.Id,
                Service: service.Name,
                FetchToken: fetchTokenKey
            ))
        );
    }
}

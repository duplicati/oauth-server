using System.Net;

namespace OAuthServer.Handler;

/// <summary>
/// Handles the revoke request
/// </summary>
public static class CompleteRevoke
{
    /// <summary>
    /// Handles a revoke request
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="appContext">The application context</param>
    /// <returns>The awaitable token</returns>
    public static async Task Handle(HttpContext context, ApplicationContext appContext)
    {
        string? authId = context.Request.Form["authid"].FirstOrDefault()
            ?? context.Request.Headers["X-AuthID"].FirstOrDefault();

        if (string.IsNullOrEmpty(authId))
            throw new UserReportedHttpException("Missing AuthID");

        // v2 tokens have no state that can be deleted
        if (authId.StartsWith("v2:"))
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync(
                appContext.Render.Revoked(new TemplateRenderers.RevokedTemplateRenderArgs(
                    "Error: The token must be revoked from the service provider. You can de-authorize the application on the storage providers website."
                ))
            );

            return;
        }

        string result = "";

        try
        {
            var parts = authId.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                result = "Error: Invalid AuthId format";
                throw new UserReportedHttpException("Invalid AuthId format");
            }

            if (appContext.Storage == null)
            {
                result = "Error: Persisted storage is not configured on server";
                throw new UserReportedHttpException("Persisted storage is not configured");
            }

            var keyId = parts[0];
            var password = parts[1];

            // Make sure the password is correct
            var entry = await appContext.Storage.GetFromKeyIdAsync(keyId, password, context.RequestAborted);

            try
            {
                await appContext.Storage.DeleteByKeyIdAsync(keyId, context.RequestAborted);
            }
            catch
            {
                result = "Error: Internal error, failed to revoke token";
                throw;
            }
        }
        catch
        {
            // Set a generic error, if no message has been set
            if (string.IsNullOrEmpty(result))
                result = "Error: Invalid AuthId";
        }

        // No errors == success
        if (string.IsNullOrWhiteSpace(result))
            result = "Token is revoked";

        context.Response.ContentType = "text/html";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await context.Response.WriteAsync(
            appContext.Render.Revoked(new TemplateRenderers.RevokedTemplateRenderArgs(
                result
            ))
        );
    }
}

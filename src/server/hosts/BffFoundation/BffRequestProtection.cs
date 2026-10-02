using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IgaMigration.BffFoundation;

public static class BffRequestProtection
{
    // Install after UseAuthentication, before every host-owned product endpoint.
    // This composes no login, logout, token, or product endpoint of its own.
    public static IApplicationBuilder UseBffRequestProtection(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var settings = context.RequestServices.GetRequiredService<BffOptions>();
        if (!settings.LiveSignInEnabled || !context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        var authentication = await context.AuthenticateAsync(BffRegistration.CookieScheme);
        if (!authentication.Succeeded || authentication.Principal is null ||
            !BffIdentity.TryReadSubject(authentication.Principal, settings.TenantId, out var subject))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        SubjectAdmission? admission;
        try
        {
            admission = await context.RequestServices.GetRequiredService<IBffSubjectAuthority>()
                .CheckAsync(subject, context.RequestAborted);
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        var now = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        string? versionText = null;
        authentication.Properties?.Items.TryGetValue("bff.securityVersion", out versionText);
        if (admission?.Subject != subject || !BffIdentity.IsAdmitted(admission, now) ||
            !long.TryParse(versionText,
                NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version != admission!.SecurityVersion)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        context.User = authentication.Principal;
        // GET/HEAD/OPTIONS are the only safe methods; host endpoints must preserve
        // their read-only semantics. TRACE/CONNECT have no application purpose.
        if (HttpMethods.IsTrace(context.Request.Method) || HttpMethods.IsConnect(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method))
        {
            try
            {
                await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await next(context);
    });
}

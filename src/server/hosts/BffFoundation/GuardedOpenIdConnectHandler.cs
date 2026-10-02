using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IgaMigration.BffFoundation;

// Gate the supported handler before it fetches metadata or redeems a code.
// Protocol processing remains entirely in the framework/Microsoft.Identity.Web.
public sealed class GuardedOpenIdConnectHandler(IOptionsMonitor<OpenIdConnectOptions> options,
    ILoggerFactory logger, HtmlEncoder htmlEncoder, UrlEncoder encoder, BffOptions settings)
    : OpenIdConnectHandler(options, logger, htmlEncoder, encoder)
{
    private bool Allowed => settings.LiveSignInEnabled && Request.IsHttps;

    public override Task<bool> HandleRequestAsync()
    {
        if (!Allowed && (Request.Path == Options.CallbackPath || Request.Path == Options.SignedOutCallbackPath ||
            Request.Path == Options.RemoteSignOutPath))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.FromResult(true);
        }
        return base.HandleRequestAsync();
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!Allowed)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        return base.HandleChallengeAsync(properties);
    }

    protected override Task<HandleRequestResult> HandleRemoteAuthenticateAsync()
    {
        if (!Allowed)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.FromResult(HandleRequestResult.Handle());
        }
        return base.HandleRemoteAuthenticateAsync();
    }

    public override Task SignOutAsync(AuthenticationProperties? properties)
    {
        if (!Allowed)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        return base.SignOutAsync(properties);
    }
}

using System.Text.Encodings.Web;
using System.Globalization;
using IdentitySessions;
using Microsoft.Extensions.DependencyInjection;
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

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!Allowed)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        var clock = Context.RequestServices.GetRequiredService<TimeProvider>();
        PendingAuthenticationChallenge? challenge;
        try
        {
            challenge = await Context.RequestServices.GetRequiredService<IPendingAuthenticationChallengeStore>()
                .CreateAsync(clock.GetUtcNow(), Context.RequestAborted);
        }
        catch (Exception)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (challenge is null)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        properties.RedirectUri = "/bff/v1/session";
        properties.Items[BffAuthenticationEvidence.ChallengeReference] = challenge.Reference;
        properties.Items[BffAuthenticationEvidence.ChallengeIssued] = challenge.IssuedUtc.ToString("O", CultureInfo.InvariantCulture);
        await base.HandleChallengeAsync(properties);
    }

    protected override async Task<HandleRequestResult> HandleRemoteAuthenticateAsync()
    {
        if (!Allowed)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return HandleRequestResult.Handle();
        }
        var result = await base.HandleRemoteAuthenticateAsync();
        if (!result.Succeeded || result.Ticket is null) return result;
        var ticket = result.Ticket;
        var clock = Context.RequestServices.GetRequiredService<TimeProvider>();
        if (!BffAuthenticationEvidence.TryReadChallenge(ticket.Properties, out var challenge) ||
            !DateTimeOffset.TryParseExact(SessionTicket.Property(ticket, SessionTicket.AuthenticatedUtc), "O", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var authenticated) ||
            authenticated.ToUnixTimeSeconds() < challenge.IssuedUtc.ToUnixTimeSeconds() || authenticated > clock.GetUtcNow() ||
            clock.GetUtcNow() - authenticated >= TimeSpan.FromMinutes(15))
            return HandleRequestResult.Fail("Authentication transaction refused.");
        try
        {
            if (!await Context.RequestServices.GetRequiredService<IPendingAuthenticationChallengeStore>()
                .TryConsumeAsync(challenge, clock.GetUtcNow(), Context.RequestAborted))
                return HandleRequestResult.Fail("Authentication transaction refused.");
        }
        catch (Exception)
        {
            return HandleRequestResult.Fail("Authentication transaction unavailable.");
        }
        var completed = clock.GetUtcNow();
        if (authenticated > completed || completed - authenticated >= TimeSpan.FromMinutes(15) ||
            challenge.IssuedUtc > completed || completed - challenge.IssuedUtc >= TimeSpan.FromMinutes(15))
            return HandleRequestResult.Fail("Authentication transaction expired.");
        var properties = new AuthenticationProperties { RedirectUri = "/bff/v1/session" };
        foreach (var name in new[] { SessionTicket.AuthenticatedUtc, SessionTicket.SecurityVersion, SessionTicket.ProviderCheckedUtc, SessionTicket.MfaCaVerified })
            properties.Items[name] = ticket.Properties.Items[name];
        return HandleRequestResult.Success(new AuthenticationTicket(ticket.Principal, properties, ticket.AuthenticationScheme));
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

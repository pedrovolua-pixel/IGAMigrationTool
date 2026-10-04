using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;

namespace IgaMigration.BffFoundation;

// Configuration contracts only. These do not attach Azure providers or activate a host.
public sealed record BffSharedProtectionContract(Guid EnvironmentId, Guid ManagedIdentityClientId,
    string BlobRingUri, string VersionlessWrappingKeyUri)
{
    public string ApplicationDiscriminator => $"IGAMigrationTool.Bff.{EnvironmentId:D}.v1";

    public void Validate()
    {
        if (EnvironmentId == Guid.Empty || ManagedIdentityClientId == Guid.Empty ||
            !ExactUri(BlobRingUri, @"[a-z0-9]{3,24}\.blob\.core\.windows\.net", "/bff-data-protection/keyring.xml") ||
            !ExactUri(VersionlessWrappingKeyUri, @"[a-z][a-z0-9-]{1,22}[a-z0-9]\.vault\.azure\.net", "/keys/bff-data-protection"))
            throw new ArgumentException("Reviewed dedicated protection bindings are required.");
    }

    private static bool ExactUri(string value, string hostPattern, string path) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        Regex.IsMatch(uri.Host, "\\A" + hostPattern + "\\z", RegexOptions.CultureInvariant) &&
        value == "https://" + uri.Host + path;
}

public sealed record BffReviewedIngressContract(string CanonicalHost, IPAddress ImmediatePeer, bool ForwardClientAddress = false)
{
    public string FixedOrigin => "https://" + CanonicalHost;

    public void Validate()
    {
        if (string.IsNullOrEmpty(CanonicalHost) || CanonicalHost != CanonicalHost.ToLowerInvariant() ||
            Uri.CheckHostName(CanonicalHost) != UriHostNameType.Dns || CanonicalHost.EndsWith('.') ||
            ImmediatePeer is null || ImmediatePeer.Equals(IPAddress.Any) || ImmediatePeer.Equals(IPAddress.IPv6Any) ||
            ImmediatePeer.Equals(IPAddress.None))
            throw new ArgumentException("An exact reviewed canonical host and immediate peer are required.");
    }
}

public static class BffReviewedIngress
{
    // Must precede HTTPS/authentication. Original host and peer are validated before forwarding.
    // This exact-address v1 seam deliberately requires new review for additional hops/ranges.
    public static IApplicationBuilder UseBffReviewedIngress(this IApplicationBuilder app, BffReviewedIngressContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        contract.Validate();
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var proto = request.Headers["X-Forwarded-Proto"];
            var client = request.Headers["X-Forwarded-For"];
            if (!contract.ImmediatePeer.Equals(context.Connection.RemoteIpAddress) ||
                request.Host.Value != contract.CanonicalHost || request.PathBase.HasValue ||
                request.Headers.ContainsKey("X-Forwarded-Host") || request.Headers.ContainsKey("X-Forwarded-Prefix") ||
                request.Headers.ContainsKey("Forwarded") || request.Headers.ContainsKey("X-Original-Host") ||
                request.Headers.ContainsKey("X-Original-Proto") || request.Headers.ContainsKey("X-Original-For") ||
                request.Headers.ContainsKey("X-Original-Prefix") || proto.Count != 1 || proto[0] != "https" ||
                (contract.ForwardClientAddress
                    ? client.Count != 1 || !IPAddress.TryParse(client[0], out _)
                    : request.Headers.ContainsKey("X-Forwarded-For")))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        });
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto |
                (contract.ForwardClientAddress ? ForwardedHeaders.XForwardedFor : ForwardedHeaders.None),
            ForwardLimit = 1,
            RequireHeaderSymmetry = contract.ForwardClientAddress
        };
        // Framework defaults remain; the precheck admits only the exact reviewed peer.
        options.KnownProxies.Add(contract.ImmediatePeer);
        app.UseForwardedHeaders(options);
        return app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps || context.Request.Host.Value != contract.CanonicalHost)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        });
    }
}

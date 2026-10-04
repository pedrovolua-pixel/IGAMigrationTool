using Microsoft.AspNetCore.Http;

namespace IgaMigration.BffFoundation;

public sealed record PublicAuthenticationOptions
{
    public required string FixedOrigin { get; init; }

    internal void Validate()
    {
        if (!Uri.TryCreate(FixedOrigin, UriKind.Absolute, out var origin) || origin.Scheme != "https" ||
            origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 ||
            origin.Fragment.Length != 0 || FixedOrigin != origin.GetLeftPart(UriPartial.Authority))
            throw new ArgumentException("An explicit canonical HTTPS origin is required.");
    }

    internal bool MatchesTransport(HttpRequest request) => request.IsHttps &&
        string.Equals("https://" + request.Host.ToUriComponent(), FixedOrigin, StringComparison.Ordinal);

    internal bool MatchesOrigin(HttpRequest request) => MatchesTransport(request) &&
        request.Headers.Origin.Count == 1 && request.Headers.Origin[0] == FixedOrigin;
}

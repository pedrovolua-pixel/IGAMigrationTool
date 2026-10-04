using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SyntheticFixPackages;

public enum FixPackageRenderIssue { MissingSnapshot, InvalidSnapshot, OutputTooLarge }

public sealed class FixPackageHtmlSnapshot
{
    internal FixPackageHtmlSnapshot(string html, string contentDigest, string packageDigest)
    {
        Html = html;
        ContentDigest = contentDigest;
        PackageDigest = packageDigest;
    }
    public string Html { get; }
    public string ContentDigest { get; }
    public string PackageDigest { get; }
}

public sealed class FixPackageRenderResult
{
    internal FixPackageRenderResult(FixPackageRenderIssue? issue, FixPackageHtmlSnapshot? snapshot)
    {
        Issue = issue;
        Snapshot = snapshot;
    }
    public FixPackageRenderIssue? Issue { get; }
    public FixPackageHtmlSnapshot? Snapshot { get; }
    public bool Succeeded => Issue is null && Snapshot is not null;
}

/// <summary>Deterministic credential-free fictional fixture rendering; no execution or approval surface.</summary>
public static class FixPackageHtmlRenderer
{
    public const int MaximumHtmlBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Start = "<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>Fictional fix-package preview</title><style>html{color:#17212b;background:#fff;font:1rem/1.6 system-ui,sans-serif}body{margin:0}main,header,nav{max-width:70rem;margin:auto;padding:1rem}*{box-sizing:border-box;min-width:0}h1,h2,h3,h4,p,li,dt,dd,summary,code{overflow-wrap:anywhere}a{color:#003f86}a:focus-visible,summary:focus-visible{outline:3px solid #003f86;outline-offset:4px}details{margin:1rem 0;border:1px solid #687787;padding:.75rem}summary{cursor:pointer;font-weight:700}dl{margin:.5rem 0}dt{font-weight:700}dd{margin:0 0 .75rem 1rem}ol,ul{padding-left:1.5rem}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f4f7;padding:.75rem}code{font-size:.9rem}.warning{border-left:.3rem solid #8a3500;padding:.75rem;background:#fff5e8}.skip{display:inline-block;padding:.75rem}</style></head><body><a class=\"skip\" href=\"#main\">Skip to preview</a><header><h1>Fictional fix-package preview</h1>";
    private const string Main = "</header><nav aria-label=\"Preview sections\"><a href=\"#packages\">Packages</a> · <a href=\"#historical\">Historical guidance</a> · <a href=\"#provenance\">Source and integrity</a></nav><main id=\"main\" tabindex=\"-1\">";

    public static FixPackageRenderResult Render(FixPackageSnapshot? snapshot)
    {
        if (snapshot is null) return Deny(FixPackageRenderIssue.MissingSnapshot);
        try
        {
            var rebuilt = FixPackageBuilder.Build(snapshot.Guidance);
            if (!rebuilt.Succeeded) return Deny(rebuilt.Issue == FixPackageIssue.OutputTooLarge ? FixPackageRenderIssue.OutputTooLarge : FixPackageRenderIssue.InvalidSnapshot);
            var actual = FixPackageBuilder.CanonicalPayload(snapshot);
            var expected = FixPackageBuilder.CanonicalPayload(rebuilt.Snapshot!);
            if (snapshot.CanonicalJson is null || snapshot.ContentDigest is null ||
                !actual.AsSpan().SequenceEqual(expected) ||
                !actual.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(snapshot.CanonicalJson)) ||
                !string.Equals(Hash(actual), snapshot.ContentDigest, StringComparison.Ordinal))
                return Deny(FixPackageRenderIssue.InvalidSnapshot);

            var html = new Html();
            html.Add(Start);
            html.Add("<p class=\"warning\">"); html.Text(snapshot.Disclaimer); html.Add("</p><p>Status: "); html.Text(snapshot.Status); html.Add("</p>");
            html.Add(Main);
            html.Add("<section><h2>Current preview boundary</h2>");
            html.List(snapshot.Warnings); html.List(snapshot.UnavailableSections); html.Add("</section>");
            html.Add("<section id=\"packages\"><h2>Fictional packages — Unverified</h2>");
            if (snapshot.Packages.IsEmpty) html.Add("<p class=\"warning\">No findings were supplied; no fix packages or actions are available. Empty input does not establish a healthy environment.</p>");
            foreach (var package in snapshot.Packages)
            {
                html.Add("<details><summary>Package for finding "); html.Text(package.FindingId); html.Add("</summary>");
                html.Field("Package ID", package.PackageId); html.Field("Finding ID", package.FindingId);
                foreach (var option in package.Options)
                {
                    html.Add("<details><summary>Option "); html.Text(option.ScopedOptionId); html.Add(" — Unverified artifacts</summary>");
                    html.Field("Scoped option ID", option.ScopedOptionId);
                    foreach (var artifact in option.Artifacts)
                    {
                        html.Add("<article><h3>"); html.Text(artifact.Kind); html.Add(" — "); html.Text(artifact.Status); html.Add("</h3>");
                        html.Field("Artifact ID", artifact.ArtifactId); html.Field("Template ID", artifact.TemplateId);
                        html.Field("Kind", artifact.Kind); html.Field("Artifact status", artifact.Status);
                        html.Code(artifact.Text); html.Add("</article>");
                    }
                    html.Add("</details>");
                }
                html.Add("</details>");
            }
            html.Add("</section><section><h2>Fixed fictional templates</h2>");
            foreach (var template in snapshot.Templates)
            {
                html.Add("<details><summary>"); html.Text(template.Kind); html.Add(" template — Unverified</summary>");
                html.Field("Template ID", template.TemplateId); html.Field("Kind", template.Kind); html.Code(template.Text); html.Add("</details>");
            }
            html.Add("</section><section id=\"historical\"><h2>Historical upstream guidance</h2><p>These source warnings and unavailable sections describe the earlier guidance projection. The current fictional preview boundary is stated above. Finding decisions do not review artifacts.</p>");
            html.Field("Guidance schema", snapshot.Guidance.SchemaVersion); html.Field("Guidance status", snapshot.Guidance.Status);
            html.Add("<details><summary>Historical upstream warnings and unavailable sections</summary>");
            html.List(snapshot.Guidance.Warnings); html.List(snapshot.Guidance.UnavailableSections); html.Add("</details>");
            foreach (var finding in snapshot.Guidance.Findings)
            {
                html.Add("<details><summary>"); html.Text(finding.PresentationTitle); html.Add(" — finding state: "); html.Text(finding.CurrentState); html.Add("</summary>");
                html.Tree(JsonSerializer.SerializeToElement(finding, JsonOptions)); html.Add("</details>");
            }
            html.Add("</section><section id=\"provenance\"><h2>Source and integrity</h2><details><summary>Source bindings, frozen versions and locks</summary>");
            html.Tree(JsonSerializer.SerializeToElement(snapshot.Guidance.Source, JsonOptions)); html.Add("</details><details><summary>Preview versions and digests</summary>");
            html.Field("Preview schema", snapshot.SchemaVersion); html.Field("Template version", snapshot.TemplateVersion);
            html.Field("Template digest", snapshot.TemplateDigest); html.Field("Guidance digest", snapshot.Guidance.ContentDigest); html.Field("Package digest", snapshot.ContentDigest);
            html.Add("</details></section></main></body></html>\n");
            var output = html.ToString();
            return new(null, new(output, Hash(Encoding.UTF8.GetBytes(output)), snapshot.ContentDigest));
        }
        catch (HtmlLimitException) { return Deny(FixPackageRenderIssue.OutputTooLarge); }
        catch (FixPackageBuilder.OutputLimitException)
        { return Deny(FixPackageRenderIssue.OutputTooLarge); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or OverflowException or NullReferenceException)
        { return Deny(FixPackageRenderIssue.InvalidSnapshot); }
    }

    private static FixPackageRenderResult Deny(FixPackageRenderIssue issue) => new(issue, null);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class HtmlLimitException : Exception;
    private sealed class Html
    {
        private readonly StringBuilder builder = new();
        private long bytes;
        internal void Add(string value)
        {
            bytes += Encoding.UTF8.GetByteCount(value);
            if (bytes > MaximumHtmlBytes) throw new HtmlLimitException();
            builder.Append(value);
        }
        internal void Text(string value) => Add(HtmlEncoder.Default.Encode(value));
        internal void Field(string name, string value)
        { Add("<dl><dt>"); Text(name); Add("</dt><dd>"); Text(value); Add("</dd></dl>"); }
        internal void Code(string value) { Add("<pre><code>"); Text(value); Add("</code></pre>"); }
        internal void List(IEnumerable<string> values)
        { Add("<ul>"); foreach (var value in values) { Add("<li>"); Text(value); Add("</li>"); } Add("</ul>"); }
        internal void Tree(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    Add("<dl>");
                    foreach (var property in value.EnumerateObject())
                    { Add("<dt>"); Text(property.Name); Add("</dt><dd>"); Tree(property.Value); Add("</dd>"); }
                    Add("</dl>"); break;
                case JsonValueKind.Array:
                    Add("<ol>"); foreach (var item in value.EnumerateArray()) { Add("<li>"); Tree(item); Add("</li>"); }
                    Add("</ol>"); break;
                case JsonValueKind.String: Text(value.GetString()!); break;
                case JsonValueKind.Null: Add("Not supplied"); break;
                default: Text(value.GetRawText()); break;
            }
        }
        public override string ToString() => builder.ToString();
    }
}

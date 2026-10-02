using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SyntheticAiPreview;

public static class SyntheticAiPreviewRenderer
{
    public static PreviewRenderResult Render(SyntheticAiPreviewSnapshot? snapshot)
    {
        if (snapshot is null) return PreviewRenderResult.Denied(PreviewRenderIssue.MissingSnapshot);
        try
        {
            if (!Valid(snapshot)) return PreviewRenderResult.Denied(PreviewRenderIssue.InvalidSnapshot);
            var html = new StringBuilder();
            html.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            html.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">\n<title>Synthetic AI proposal preview</title>\n");
            html.Append("<style>html{color:#172b4d;background:#fff;font-family:system-ui,sans-serif;line-height:1.6}body{margin:0;padding:1rem}main{max-width:70rem;margin:auto}h1,h2,h3,h4,p,dt,dd,li{overflow-wrap:anywhere}h1{font-size:2rem}h2{font-size:1.5rem}h3{font-size:1.2rem}h4{font-size:1rem}section,article{margin-block:1.5rem}article{border-top:2px solid #526174;padding-top:1rem}dl{display:grid;grid-template-columns:minmax(0,12rem) minmax(0,1fr);gap:.5rem 1rem}dt{font-weight:700}dd{margin:0;min-width:0}ol,ul{padding-left:1.5rem}li{margin-block:.5rem}.disclaimer{border:2px solid #526174;padding:1rem;font-weight:600}a{color:#0645ad}a:focus-visible,[tabindex]:focus{outline:3px solid #814600;outline-offset:3px}.skip{position:absolute;left:1rem;top:-8rem;background:#fff;padding:.5rem}.skip:focus{top:1rem}nav{display:flex;flex-wrap:wrap;gap:1rem}@media(max-width:30rem){dl{grid-template-columns:minmax(0,1fr)}dd{margin-bottom:.5rem}h1{font-size:1.6rem}}</style>\n</head>\n<body>\n");
            html.Append("<a class=\"skip\" href=\"#preview-content\">Skip to preview content</a>\n<main id=\"preview-content\" tabindex=\"-1\">\n<h1>Synthetic AI proposal preview</h1>\n<p class=\"disclaimer\">");
            Text(html, snapshot.Disclaimer);
            html.Append("</p>\n<p>Status: Proposed</p>\n<nav aria-label=\"Preview sections\"><a href=\"#source\">Source</a><a href=\"#proposals\">Proposals</a></nav>\n<section id=\"source\">\n<h2>Source</h2>\n<dl>\n");
            var source = snapshot.Source;
            Source(html, "Customer", source.CustomerId);
            Source(html, "Project", source.ProjectId);
            Source(html, "Environment", source.EnvironmentId);
            Source(html, "Run ID", source.RunId.ToString("D"));
            Source(html, "Baseline digest", source.BaselineDigest);
            Source(html, "Profile digest", source.ProfileDigest);
            Source(html, "Normalization version", source.NormalizationVersion);
            Source(html, "Redaction version", source.RedactionVersion);
            Source(html, "Prompt version", source.PromptVersion);
            Source(html, "Packet digest", snapshot.PacketDigest);
            Source(html, "Proposal digest", snapshot.ProposalDigest);
            Source(html, "Preview digest", snapshot.ContentDigest);
            html.Append("</dl>\n</section>\n<section id=\"proposals\">\n<h2>Proposals</h2>\n");
            if (snapshot.Proposals.IsEmpty)
                html.Append("<p>No proposals were returned. This does not establish healthy or complete assessment coverage.</p>\n");
            foreach (var proposal in snapshot.Proposals)
            {
                html.Append("<article>\n<h3>Proposal ");
                Text(html, proposal.ProposalId);
                html.Append("</h3>\n<p>Proposed and untrusted. Cited statements are not verified facts.</p>\n");
                Statements(html, "Facts", proposal.Facts);
                Statements(html, "Inferences", proposal.Inferences);
                Statements(html, "Assumptions", proposal.Assumptions);
                Statements(html, "Suggestions", proposal.Suggestions);
                html.Append("<section>\n<h4>Missing context</h4>\n");
                List(html, proposal.MissingContext, "No missing context was declared.");
                html.Append("</section>\n<section>\n<h4>Uncertainty</h4>\n<p>");
                Text(html, proposal.Uncertainty.Length == 0 ? "No uncertainty was declared." : proposal.Uncertainty);
                html.Append("</p>\n</section>\n<section>\n<h4>Conflicting evidence IDs</h4>\n");
                List(html, proposal.ConflictingEvidenceIds, "No conflicting evidence was declared.");
                html.Append("</section>\n</article>\n");
            }
            html.Append("</section>\n</main>\n</body>\n</html>");
            var output = html.ToString();
            if (Encoding.UTF8.GetByteCount(output) > PreviewCanonical.MaximumBytes)
                return PreviewRenderResult.Denied(PreviewRenderIssue.OutputTooLarge);
            return PreviewRenderResult.Accepted(new PreviewHtmlSnapshot(output, PreviewCanonical.Digest(output), snapshot.ContentDigest));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            return PreviewRenderResult.Denied(PreviewRenderIssue.InvalidSnapshot);
        }
    }

    private static bool Valid(SyntheticAiPreviewSnapshot snapshot)
    {
        if (snapshot.Source is not { } source || snapshot.Proposals.IsDefault || snapshot.Proposals.Length > 16 ||
            snapshot.CanonicalJson is null || Encoding.UTF8.GetByteCount(snapshot.CanonicalJson) > PreviewCanonical.MaximumBytes ||
            !Digest(snapshot.ContentDigest) || !Digest(snapshot.PacketDigest) || !Digest(snapshot.ProposalDigest) ||
            source.CustomerId != "synthetic-customer" || source.ProjectId != "synthetic-project" ||
            source.EnvironmentId != "synthetic-environment" || source.RunId == Guid.Empty ||
            !Digest(source.BaselineDigest) || !Digest(source.ProfileDigest) ||
            source.NormalizationVersion != "fixture-normalization-v1" || source.RedactionVersion != "fixture-redaction-v1" ||
            source.PromptVersion != "fixture-prompt-v1") return false;
        foreach (var proposal in snapshot.Proposals)
        {
            if (proposal is null || proposal.ProposalId is null || proposal.Uncertainty is null ||
                proposal.Facts.IsDefault || proposal.Inferences.IsDefault || proposal.Assumptions.IsDefault ||
                proposal.Suggestions.IsDefault || proposal.MissingContext.IsDefault || proposal.ConflictingEvidenceIds.IsDefault ||
                proposal.MissingContext.Any(value => value is null) || proposal.ConflictingEvidenceIds.Any(value => value is null)) return false;
            foreach (var statements in new[] { proposal.Facts, proposal.Inferences, proposal.Assumptions, proposal.Suggestions })
                foreach (var statement in statements)
                    if (statement is null || statement.Text is null || statement.EvidenceIds.IsDefault || statement.RuleIds.IsDefault ||
                        statement.EvidenceIds.Any(value => value is null) || statement.RuleIds.Any(value => value is null)) return false;
        }
        return PreviewCanonical.Digest(snapshot.CanonicalJson) == snapshot.ContentDigest &&
            PreviewCanonical.Payload(snapshot) == snapshot.CanonicalJson;
    }

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Text(StringBuilder html, string value) => html.Append(WebUtility.HtmlEncode(value));
    private static void Source(StringBuilder html, string label, string value)
    {
        html.Append("<dt>").Append(label).Append("</dt><dd>");
        Text(html, value);
        html.Append("</dd>\n");
    }
    private static void List(StringBuilder html, ImmutableArray<string> values, string empty)
    {
        if (values.IsEmpty) { html.Append("<p>").Append(empty).Append("</p>\n"); return; }
        html.Append("<ul>\n");
        foreach (var value in values) { html.Append("<li>"); Text(html, value); html.Append("</li>\n"); }
        html.Append("</ul>\n");
    }
    private static void Statements(StringBuilder html, string label, ImmutableArray<PreviewStatement> statements)
    {
        html.Append("<section>\n<h4>").Append(label).Append("</h4>\n");
        if (statements.IsEmpty) html.Append("<p>No statements were supplied in this category.</p>\n");
        else
        {
            html.Append("<ol>\n");
            foreach (var statement in statements)
            {
                html.Append("<li>\n<p>"); Text(html, statement.Text); html.Append("</p>\n<p>Evidence IDs</p>\n");
                List(html, statement.EvidenceIds, "No evidence IDs were supplied.");
                html.Append("<p>Rule IDs</p>\n");
                List(html, statement.RuleIds, "No rule IDs were supplied.");
                html.Append("</li>\n");
            }
            html.Append("</ol>\n");
        }
        html.Append("</section>\n");
    }
}

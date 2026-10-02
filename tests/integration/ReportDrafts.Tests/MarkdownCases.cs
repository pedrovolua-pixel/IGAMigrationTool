using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ReportDrafts;

internal static class MarkdownCases
{
    internal static void Run(DraftReportSnapshot snapshot)
    {
        var markdown = StructuredDraftMarkdown.Render(snapshot);
        Check.Equal(markdown.Version, "synthetic-draft-markdown-v1", "explicit Markdown version");
        Check.Equal(markdown.CanonicalContentDigest, snapshot.CanonicalContentDigest, "Markdown forwards exact single-source digest");
        Check.Equal(markdown.MarkdownSha256, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(markdown.MarkdownText))), "actual UTF8 artifact hash independently computed");
        Check.Equal(StructuredDraftMarkdown.Render(snapshot), markdown, "identical immutable value yields identical bytes");
        foreach (var literal in new[]
        {
            "# Synthetic draft report — unpublished\n", "The run remains Scoring.",
            "## source — Source bindings", "## summary — Health summary",
            "### provisional — Provisional health", "### publishableCurrent — Publishable-current health (unpublished)",
            "## categories — Category health", "## objectTypes — Object-type health", "## modules — Module health",
            "## outcomes — Outcome adherence", "## findings — Current findings and generated originals",
            "## healthyControls — Healthy synthetic controls", "## quality — Separate assessment quality",
            "## limitations — Coverage limitations", "## maturity — Independent capability maturity",
            "## reviewHistory — Captured review history", "## methodology — Methodology", "## unavailableSections — Unavailable sections",
            "| Binding | Value |", "| Field | Value |", "Canonical content digest", snapshot.CanonicalContentDigest,
            "Health and quality are separate. Gaps never imply healthy controls."
        }) Check.That(markdown.MarkdownText.Contains(literal, StringComparison.Ordinal), $"independent required Markdown literal: {literal}");
        foreach (var binding in new[] { snapshot.Source.RunId.ToString("D"), snapshot.Source.RunInputDigest,
            snapshot.Source.AnalysisContentDigest, snapshot.Source.ScoringContentDigest, snapshot.Source.ReviewSnapshotDigest,
            snapshot.Source.MaturityFixtureDigest, snapshot.Source.MaturityInputDigest, snapshot.Source.MaturityContentDigest })
        {
            var expected = binding.Contains('-') ? binding.Replace("-", "\\-", StringComparison.Ordinal) : binding;
            Check.That(markdown.MarkdownText.Contains(expected, StringComparison.Ordinal), "literal immutable source binding rendered");
        }
        Denied(() => StructuredDraftMarkdown.Render(snapshot with { CanonicalContentDigest = new string('0', 64) }));
        Denied(() => StructuredDraftMarkdown.Render(snapshot with { Status = "Published" }));
        Denied(() => StructuredDraftMarkdown.Render(snapshot with { SchemaVersion = "unknown-report-version" }));
        try { StructuredDraftMarkdown.Render(null!); throw new InvalidOperationException("Null render produced text"); }
        catch (ArgumentNullException) { Check.That(true, "null renderer input refuses output"); }
        Check.Group("DR-MD-001 independent section/source/hash literals, deterministic bytes and invalid-snapshot refusal");
    }
    internal static void Hostile(DraftReportSnapshot snapshot)
    {
        var text = StructuredDraftMarkdown.Render(snapshot).MarkdownText;
        foreach (var literal in new[] { "&lt;script&gt;", "&amp;", "\\[link\\]\\(https\\:\\/\\/evil\\.invalid", "\\!\\[img\\]", "\\`\\`\\`sh", " ↵ ", " [tab] ", "[U+202E]" })
            Check.That(text.Contains(literal, StringComparison.Ordinal), $"independent hostile encoded fragment {literal}");
        Check.That(!text.Contains("<script", StringComparison.OrdinalIgnoreCase) && !text.Contains("<img", StringComparison.OrdinalIgnoreCase), "no raw source HTML");
        Check.That(!text.Contains("https://evil.invalid", StringComparison.Ordinal) && !text.Contains("![img](", StringComparison.Ordinal), "source URL/image cannot become live Markdown");
        Check.That(!Regex.IsMatch(text, "(?m)^(?:```|~~~|## stolen|<script)"), "source multiline text cannot create fences/headings/HTML block");
        Check.Group("DR-MD-002 independently expected hostile HTML/link/image/fence/newline/control escaping");
    }
    private static void Denied(Action action)
    {
        try { action(); throw new InvalidOperationException("Invalid snapshot emitted Markdown"); }
        catch (ArgumentException) { Check.That(true, "invalid snapshot refuses Markdown"); }
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportDrafts;

internal static class Program
{
    private static int checks;
    private static void Main()
    {
        var input = MarkdownFixture.Input();
        var snapshot = MarkdownFixture.Build(input);
        var rendered = StructuredDraftMarkdown.Render(snapshot);
        Equal(rendered.Version, "synthetic-draft-markdown-v1", "exact Markdown version");
        Equal(rendered.CanonicalContentDigest, snapshot.CanonicalContentDigest, "same validated canonical source identity");
        Equal(rendered.MarkdownSha256, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rendered.MarkdownText))), "independent exact UTF8 artifact digest");
        Equal(StructuredDraftMarkdown.Render(snapshot), rendered, "identical snapshot yields identical result and bytes");
        That(rendered.MarkdownText.StartsWith("# Synthetic draft report — unpublished\n\n", StringComparison.Ordinal), "prominent synthetic/unpublished heading");
        That(rendered.MarkdownText.EndsWith('\n') && !rendered.MarkdownText.Contains('\r') && !rendered.MarkdownText.StartsWith('\uFEFF'), "LF deterministic text without BOM");
        foreach (var literal in new[]
        {
            "The run remains Scoring.", "Canonical content digest", snapshot.CanonicalContentDigest,
            "provisional — Provisional health", "publishableCurrent — Publishable-current health (unpublished)",
            "| display | 44\\.2 |", "| raw | 78\\.3 |", "| status | Red |", "| status | Yellow |",
            "| eligibleUnits | 2 |", "Critical review pending", "Synthetic draft only", "Original synthetic title",
            "Current synthetic title", "| state | Proposed |", "| severity | Critical |", "| confidencePercent | 100 |",
            "Deterministic synthetic evidence", "Explicit fixture count is one", "Fictional inference", "Fixture assumptions only",
            "Unverified fictional guidance", "fixture\\:object\\-a\\:SYN\\-GUARD", "| level | Managed |",
            "| mandatoryDomains | 1 |", "| improvementMissingDistinctAssessments | 1 |", "| gapUnits | 1 |",
            "| plannedUnits | 3 |", "| executedUnits | 2 |", "InsufficientEvidence", "fixture\\-facts\\-missing", "object\\-pass", "| value\\[0\\]\\.state | Pass |",
            "Risk acceptance is unavailable", "Reassessment is unavailable", "Publication is unavailable",
            "synthetic\\-review\\-maturity\\-equal\\-v1", "synthetic\\-review\\-maturity\\-app\\-v1",
            "10000000\\-0000\\-4000\\-8000\\-000000000001", "| runRevision | 4 |", "| reviewRunRevision | 4 |",
            "scoringContentDigest", "savedCoverageDigest", "maturityFixtureDigest", "reviewSnapshotDigest",
            "null \\(not supplied\\)", "pilot\\-health\\-v1", "pilot\\-maturity\\-v1"
        }) Contains(rendered.MarkdownText, literal, "literal value/source/quality/maturity/original parity");
        var position = -1;
        foreach (var section in new[] { "source", "summary", "categories", "objectTypes", "modules", "outcomes", "findings", "healthyControls", "quality", "limitations", "maturity", "reviewHistory", "methodology", "unavailableSections" })
        {
            var next = rendered.MarkdownText.IndexOf("## " + section + " — ", StringComparison.Ordinal);
            That(next > position, "stable ordered section identifier " + section); position = next;
        }
        Contains(rendered.MarkdownText, "### finding:" + MarkdownFixture.FindingId + "\n\n", "stable finding identity");
        Contains(rendered.MarkdownText, "### review:" + MarkdownFixture.FindingId + "\n\n", "review links same finding identity");
        That(!rendered.MarkdownText.Contains("```", StringComparison.Ordinal) && !rendered.MarkdownText.Contains('<'), "no executable fences or raw HTML");
        Console.WriteLine("PASS M6-001 literal sections/scores/source/originals/history/quality/maturity and digest parity");

        var hostile = "<script>alert(\"x\")</script>\r\n# injected\n```sh\n$(touch /tmp/x)\n```\n[click](https://evil.example/x) ![img](//evil.example/x) www.example.com p@evil.example =SUM(A1)|x & &#60; \u202E\t\0";
        var hostileInput = MarkdownFixture.Change(input, node => node["warnings"]![0] = hostile);
        var hostileOutput = StructuredDraftMarkdown.Render(MarkdownFixture.Build(hostileInput)).MarkdownText;
        foreach (var literal in new[]
        {
            "&lt;script&gt;alert\\(\\\"x\\\"\\)&lt;\\/script&gt;", " ↵ \\# injected ↵ ",
            "\\`\\`\\`sh", "\\$\\(touch \\/tmp\\/x\\)", "\\[click\\]\\(https\\:\\/\\/evil\\.example\\/x\\)",
            "\\!\\[img\\]\\(\\/\\/evil\\.example\\/x\\)", "www\\.example\\.com", "p\\@evil\\.example",
            "\\=SUM\\(A1\\)\\|x", "&amp; &amp;\\#60\\;", "[U+202E] [tab] [U+0000]"
        }) Contains(hostileOutput, literal, "independent hostile text escape golden");
        foreach (var forbidden in new[] { "<script", "```", "https://", "//evil", "www.example", "p@evil", "[click](", "![img]", "\n# injected", "\n$(", "\u202E", "\0" })
            That(!hostileOutput.Contains(forbidden, StringComparison.Ordinal), "no active or structural source token " + forbidden);
        Equal(Count(hostileOutput, "\n## "), 14, "source newlines cannot inject sections");
        Console.WriteLine("PASS M6-002 literal HTML/link/image/fence/autolink/shell/formula/control escaping");

        var historyInput = MarkdownFixture.Change(input, node =>
        {
            node["findings"]![0]!["revision"] = 3; node["findings"]![0]!["state"] = "Confirmed"; node["findings"]![0]!["reviewRequired"] = false;
            node["reviewHistory"]![0]!["revision"] = 3;
            node["reviewHistory"]![0]!["events"] = JsonNode.Parse("""
            [{"eventId":"20000000-0000-4000-8000-000000000003","actorId":"synthetic-edit-actor","actorRoles":["Consultant"],"kind":"EditPresentation","recordedAtUtc":"2026-10-02T11:00:00Z","revision":1,"state":"Proposed","reason":null,"text":null,"title":"Current synthetic title","businessContext":"Fictional context"},
             {"eventId":"20000000-0000-4000-8000-000000000001","revision":2,"actorId":"synthetic-first-actor","recordedAtUtc":"2026-10-02T12:00:00Z","kind":"Confirm","state":"Confirmed","actorRoles":["Consultant"],"reason":null,"text":null,"title":null,"businessContext":null},
             {"eventId":"20000000-0000-4000-8000-000000000002","revision":3,"actorId":"synthetic-second-actor","recordedAtUtc":"2026-10-02T12:01:00Z","kind":"Comment","state":"Confirmed","actorRoles":["Consultant"],"text":"Recorded fictional comment","reason":null,"title":null,"businessContext":null}]
            """);
        });
        var history = StructuredDraftMarkdown.Render(MarkdownFixture.Build(historyInput));
        Contains(history.MarkdownText, "Recorded fictional comment", "attributed immutable comment appears");
        That(history.MarkdownText.IndexOf("synthetic\\-first\\-actor", StringComparison.Ordinal) < history.MarkdownText.IndexOf("synthetic\\-second\\-actor", StringComparison.Ordinal), "history retains revision order");
        Contains(history.MarkdownText, "events\\[1\\]\\.revision | 2", "first captured event revision");
        Contains(history.MarkdownText, "events\\[2\\]\\.revision | 3", "second captured event revision");
        That(history.CanonicalContentDigest != rendered.CanonicalContentDigest && history.MarkdownSha256 != rendered.MarkdownSha256, "changed current/history has new identity");
        Equal(StructuredDraftMarkdown.Render(snapshot), rendered, "later current state cannot rewrite prior returned draft");
        Console.WriteLine("PASS M6-003 captured ordered history and retained old value");

        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var name in new[] { "fr-FR", "ar-SA", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name); CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                Equal(StructuredDraftMarkdown.Render(MarkdownFixture.Build(input)), rendered, "culture independent " + name);
            }
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
        var reversed = new JsonObject();
        foreach (var property in JsonNode.Parse(input.Content.GetRawText())!.AsObject().Reverse()) reversed.Add(property.Key, property.Value?.DeepClone());
        Equal(StructuredDraftMarkdown.Render(MarkdownFixture.Build(input with { Content = MarkdownFixture.Json(reversed.ToJsonString()) })), rendered, "producer object-order independent Markdown");
        var sets = MarkdownFixture.Change(input, node => node["healthyControls"]!.AsArray().Add(JsonNode.Parse("""
        {"objectId":"object-z","ruleId":"SYN-TRACE","ruleVersion":"synthetic-rule-v1","state":"Pass"}
        """)));
        var canonicalSets = MarkdownFixture.Build(sets);
        var reversedSets = MarkdownFixture.Change(new(canonicalSets.Source, canonicalSets.Content), node =>
        {
            var array = node["healthyControls"]!.AsArray();
            var records = array.Reverse().Select(item => item!.DeepClone()).ToArray();
            array.Clear(); foreach (var item in records) array.Add(item);
        });
        Equal(StructuredDraftMarkdown.Render(MarkdownFixture.Build(reversedSets)), StructuredDraftMarkdown.Render(canonicalSets), "producer keyed-array order cannot change canonical Markdown bytes");
        Throws<ArgumentException>(() => StructuredDraftMarkdown.Render(canonicalSets with { Content = reversedSets.Content }), "noncanonical snapshot representation cannot reuse canonical digest");
        using (var document = JsonDocument.Parse(input.Content.GetRawText()))
        {
            var detached = MarkdownFixture.Build(input with { Content = document.RootElement });
            document.Dispose();
            Equal(StructuredDraftMarkdown.Render(detached), rendered, "source document disposal cannot change result");
        }
        Throws<ArgumentNullException>(() => StructuredDraftMarkdown.Render(null!), "null snapshot refused");
        Throws<ArgumentException>(() => StructuredDraftMarkdown.Render(snapshot with { CanonicalContentDigest = new string('0', 64) }), "tampered digest refused");
        Throws<ArgumentException>(() => StructuredDraftMarkdown.Render(snapshot with { SchemaVersion = "unknown" }), "unknown schema refused");
        Throws<ArgumentException>(() => StructuredDraftMarkdown.Render(snapshot with { Status = "Published" }), "publication cannot be inferred");
        Throws<ArgumentException>(() => StructuredDraftMarkdown.Render(snapshot with { Content = hostileInput.Content }), "changed content with old digest refused");
        Throws<ArgumentException>(() => StructuredDraftMarkdown.Render(snapshot with { Source = snapshot.Source with { ReviewRunId = Guid.NewGuid() } }), "cross-run source refused");
        Console.WriteLine("PASS M6-004 culture/order/detachment and invalid-snapshot fail-closed");
        Console.WriteLine($"{checks} Markdown assertions passed. Pure SYNTHETIC text only; PDF, export, publication, host/UI and manual accessibility NOT VERIFIED.");
    }

    private static int Count(string value, string fragment) => value.Split(fragment, StringSplitOptions.None).Length - 1;
    private static void Contains(string value, string expected, string message) => That(value.Contains(expected, StringComparison.Ordinal), message + "; missing " + expected);
    private static void Equal<T>(T actual, T expected, string message) => That(EqualityComparer<T>.Default.Equals(actual, expected), message);
    private static void That(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { checks++; return; }
        throw new InvalidOperationException(message);
    }
}

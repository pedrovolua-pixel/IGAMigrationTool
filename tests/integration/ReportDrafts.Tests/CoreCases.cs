using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportDrafts;

internal static class CoreCases
{
    internal static DraftReportSnapshot Accept(DraftReportInput input)
    {
        var result = DraftSnapshotBuilder.Build(input);
        Check.That(result.Succeeded && result.Snapshot is not null, $"valid independent input accepted, actual {result.Issue}"); return result.Snapshot!;
    }
    private static void Denied(DraftReportInput? input, string message)
    {
        var result = DraftSnapshotBuilder.Build(input);
        Check.That(result.Issue is not null && result.Snapshot is null && !result.Succeeded, $"fail-closed {message}");
    }
    internal static void Run()
    {
        var input = CoreFixture.Create(); var snapshot = Accept(input);
        Check.Equal(snapshot.SchemaVersion, "synthetic-draft-report-v1", "fixed draft schema");
        Check.Equal(snapshot.Status, "SyntheticDraft", "draft never claims publication");
        Check.Equal(snapshot.CanonicalContentDigest, "a48a9aefe6efcfa864812f6ba7b84211877a73df9e519704a114561dc31d07e9", "independently pinned complete canonical digest");
        Check.Equal(Encoding.UTF8.GetString(DraftSnapshotBuilder.CanonicalPayload(snapshot)), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "canonical-golden.json")), "independent complete canonical byte golden");
        Check.Equal(snapshot.Content.GetProperty("provisional").GetProperty("display").GetString(), "50.0", "one equal-weight pass and proposed Critical provisional literal");
        Check.Equal(snapshot.Content.GetProperty("publishableCurrent").GetProperty("display").GetString(), "100.0", "proposed excluded, one pass current literal");
        Check.Equal(snapshot.Content.GetProperty("maturity").GetProperty("level").GetString(), "Initial", "health never fills missing maturity");
        Check.Equal(snapshot.Content.GetProperty("maturity").GetProperty("insufficientIndicators").GetInt32(), 10, "all ten indicators remain insufficient");
        Check.That(snapshot.Content.GetProperty("categories").EnumerateArray().Select(row => row.GetProperty("id").GetString()).SequenceEqual(new[] { "OPERATIONS", "SECURITY" }), "ordinal category ID ordering independent literal");
        MarkdownCases.Run(snapshot);
        Check.Group("DR-CORE-001 independent complete byte/digest/score/maturity/order literals");

        var reversed = input with
        {
            Source = input.Source with
            {
                FrozenVersions = ReverseProperties(input.Source.FrozenVersions),
                AnalysisLock = ReverseProperties(input.Source.AnalysisLock),
                CapabilityLock = CoreFixture.Change(ReverseProperties(input.Source.CapabilityLock), node => ReverseArray(node, "modules"))
            },
            Content = CoreFixture.Change(ReverseProperties(input.Content), node =>
            {
                foreach (var key in new[] { "categories", "modules", "findings", "reviewHistory", "healthyControls", "limitations" }) ReverseArray(node, key);
                ReverseArray(node["maturity"]!.AsObject(), "domains");
            })
        };
        Check.Equal(Accept(reversed).CanonicalContentDigest, snapshot.CanonicalContentDigest, "reordering declared sets and object properties retains whole digest");
        var oldCulture = CultureInfo.CurrentCulture; var oldUi = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR"); Check.Equal(Accept(input).CanonicalContentDigest, snapshot.CanonicalContentDigest, "culture cannot alter canonical bytes or decimal strings"); }
        finally { CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUi; }
        DraftReportSnapshot retained;
        using (var contentDoc = JsonDocument.Parse(input.Content.GetRawText()))
        using (var versionDoc = JsonDocument.Parse(input.Source.FrozenVersions.GetRawText()))
        using (var capDoc = JsonDocument.Parse(input.Source.CapabilityLock.GetRawText()))
        using (var analysisDoc = JsonDocument.Parse(input.Source.AnalysisLock.GetRawText()))
            retained = Accept(input with { Content = contentDoc.RootElement, Source = input.Source with { FrozenVersions = versionDoc.RootElement, CapabilityLock = capDoc.RootElement, AnalysisLock = analysisDoc.RootElement } });
        Check.Equal(Encoding.UTF8.GetString(DraftSnapshotBuilder.CanonicalPayload(retained)), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "canonical-golden.json")), "all returned JSON survives caller document disposal");
        var bytes = DraftSnapshotBuilder.CanonicalPayload(retained); bytes[0] = 0;
        Check.Equal(retained.CanonicalContentDigest, snapshot.CanonicalContentDigest, "caller-owned byte buffer cannot mutate returned snapshot");
        var changed = Accept(input with { Content = CoreFixture.Change(input.Content, node => node["warnings"]!.AsArray().Add("New synthetic interpretation warning")) });
        Check.That(changed.CanonicalContentDigest != snapshot.CanonicalContentDigest && DraftSnapshotBuilder.ValidateSnapshot(retained), "new current draft changes digest while retained value stays valid");
        foreach (var source in new[] { input.Source with { RunInputDigest = CoreFixture.Hex('a') }, input.Source with { AnalysisContentDigest = CoreFixture.Hex('a') },
            input.Source with { ScoringContentDigest = CoreFixture.Hex('a') }, input.Source with { SavedCoverageDigest = CoreFixture.Hex('a') }, input.Source with { ReviewSnapshotDigest = CoreFixture.Hex('a') } })
            Check.That(Accept(input with { Source = source }).CanonicalContentDigest != snapshot.CanonicalContentDigest, "each independent source digest contributes to canonical identity");
        Check.Group("DR-CORE-002 ordering/culture/deep clone/disposal/old returned value/source digest binding");

        Denied(null, "null input");
        foreach (var source in new[] { input.Source with { Scope = new("wrong-customer", "synthetic-project", "synthetic-environment") },
            input.Source with { Scope = new("synthetic-customer", "wrong-project", "synthetic-environment") }, input.Source with { Scope = new("synthetic-customer", "synthetic-project", "wrong-environment") },
            input.Source with { RunId = Guid.Empty }, input.Source with { RunRevision = -1 }, input.Source with { RunState = "Published" }, input.Source with { ProfileId = "synthetic-analysis-equal-v1" },
            input.Source with { ReviewRunId = Guid.NewGuid() }, input.Source with { ReviewRunRevision = 12 }, input.Source with { RunInputDigest = "bad" }, input.Source with { RunInputDigest = CoreFixture.Hex('A') },
            input.Source with { AnalysisFixtureDigest = CoreFixture.Hex('a') }, input.Source with { MaturityFixtureDigest = CoreFixture.Hex('a') }, input.Source with { BaselineId = "different-baseline" },
            input.Source with { FrozenVersions = CoreFixture.Change(input.Source.FrozenVersions, node => node["applicationVersion"] = "unknown-app") },
            input.Source with { FrozenVersions = CoreFixture.Change(input.Source.FrozenVersions, node => node.Remove("workSchemaVersion")) },
            input.Source with { CapabilityLock = CoreFixture.Change(input.Source.CapabilityLock, node => node["productBuild"] = "wrong-build") },
            input.Source with { CapabilityLock = CoreFixture.Change(input.Source.CapabilityLock, node => node["modules"]!.AsArray().Add(node["modules"]![0]!.DeepClone())) },
            input.Source with { AnalysisLock = CoreFixture.Change(input.Source.AnalysisLock, node => node["compatibility"]!["ruleLanguageVersion"] = "unknown-language") } })
            Denied(input with { Source = source }, "malformed/wrong/unknown exact source binding");
        foreach (var field in new[] { "provisional", "quality", "maturity", "reviewHistory", "methodology", "unavailableSections" })
            Denied(input with { Content = CoreFixture.Change(input.Content, node => node.Remove(field)) }, "missing required content " + field);
        foreach (var token in new[] { "actions", "allowedActions", "csrfToken", "observedAtDatabaseUtc", "reportDraft" })
            Denied(input with { Content = CoreFixture.Change(input.Content, node => node["maturity"]![token] = "must not enter draft") }, "transport/time/recursive draft token " + token);
        foreach (var field in new[] { "categories", "modules", "findings", "reviewHistory", "healthyControls" })
            Denied(input with { Content = CoreFixture.Change(input.Content, node => node[field]!.AsArray().Add(node[field]![0]!.DeepClone())) }, "duplicate declared stable ID " + field);
        foreach (var mutation in new Action<JsonObject>[]
        {
            node => node["provisional"]!["display"] = "49.9", node => node["provisional"]!["status"] = "Green", node => node["provisional"]!["raw"] = "100.01",
            node => node["provisional"]!["eligibleUnits"] = 0, node => node["quality"]!["plannedUnits"] = 99,
            node => node["maturity"]!["inputDigest"] = CoreFixture.Hex('a'), node => node["maturity"]!["gates"]!.AsArray().RemoveAt(0),
            node => node["maturity"]!["gates"]![0]!["requiredPercent"] = 59,
            node => node["maturity"]!["domains"]![0]!["indicators"]![1]!["kind"] = "Design",
            node => node["maturity"]!["domains"]![0]!["indicators"]![0]!["state"] = "unknown-state",
            node => node["maturity"]!["domains"]![0]!["indicators"]![0]!["reasonCode"] = null,
            node => node["maturity"]!["ownership"]!["state"] = "Met",
            node => node["reviewHistory"]![0]!["findingId"] = "other-finding", node => node["reviewHistory"]![0]!["revision"] = 1,
            node => node["findings"]![0]!["state"] = "AcceptedRisk", node => node["findings"]![0]!["title"] = "Edited without attributed event"
        }) Denied(input with { Content = CoreFixture.Change(input.Content, mutation) }, "invalid score/quality/nested maturity/history/current binding");
        foreach (var mutation in new Action<JsonObject>[]
        {
            node => node["provisional"]!["ordinaryUnknown"] = "extra",
            node => node["categories"]![0]!["ordinaryUnknown"] = "extra",
            node => node["objectTypes"]![0]!["ordinaryUnknown"] = "extra",
            node => node["quality"]!["ordinaryUnknown"] = "extra",
            node => node["reviewHistory"]![0]!["ordinaryUnknown"] = "extra",
            node => node["healthyControls"]![0]!["ordinaryUnknown"] = "extra",
            node => node["findings"]![0]!["ordinaryUnknown"] = "extra",
            node => node["maturity"]!["ordinaryUnknown"] = "extra"
        }) Denied(input with { Content = CoreFixture.Change(input.Content, mutation) }, "ordinary nested unknown property");
        var withLimitation = input with { Content = CoreFixture.Change(input.Content, node => node["limitations"]!.AsArray().Add(JsonNode.Parse(CoreFixture.Json(new { objectId = "OBJECT-A", ruleId = "RULE-GAP", state = "InsufficientEvidence", reasonCode = "SYNTHETIC-MISSING" }).GetRawText()))) };
        Accept(withLimitation);
        Denied(withLimitation with { Content = CoreFixture.Change(withLimitation.Content, node => node["limitations"]![0]!["ordinaryUnknown"] = "extra") }, "ordinary limitation row unknown property");
        var history = CoreFixture.WithHistory(input); Accept(history);
        Denied(CoreFixture.WithHistory(input, duplicate: true), "duplicate event UUID within one finding");
        Denied(history with { Content = CoreFixture.Change(history.Content, node => ReverseArray(node["reviewHistory"]![0]!.AsObject(), "events")) }, "history order is preserved, not silently sorted");
        Denied(input with { Content = JsonDocument.Parse(input.Content.GetRawText().Replace("\"provisional\":", "\"provisional\":null,\"provisional\":", StringComparison.Ordinal)).RootElement }, "duplicate JSON property names");
        Check.Group("DR-CORE-003 fail-closed binding/version/required data/duplicate/time/token/nested maturity/history negatives");

        var hostile = CoreFixture.WithHistory(input);
        hostile = hostile with
        {
            Content = CoreFixture.Change(hostile.Content, node =>
        {
            node["warnings"]!.AsArray().Add(CoreFixture.Hostile);
            node["reviewHistory"]![0]!["events"]![0]!["text"] = CoreFixture.Hostile;
        })
        };
        MarkdownCases.Hostile(Accept(hostile));
    }
    private static void ReverseArray(JsonObject node, string key) => node[key] = new JsonArray(node[key]!.AsArray().Reverse().Select(item => item!.DeepClone()).ToArray());
    private static JsonElement ReverseProperties(JsonElement value)
    {
        using var memory = new MemoryStream(); using (var writer = new Utf8JsonWriter(memory)) Write(writer, value);
        using var parsed = JsonDocument.Parse(memory.ToArray()); return parsed.RootElement.Clone();
        static void Write(Utf8JsonWriter writer, JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var property in element.EnumerateObject().Reverse()) { writer.WritePropertyName(property.Name); Write(writer, property.Value); } writer.WriteEndObject(); }
            else if (element.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
            else element.WriteTo(writer);
        }
    }
}

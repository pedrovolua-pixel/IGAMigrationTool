using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RecommendationGuidance;
using ReportDrafts;

internal static class CoreCases
{
    internal static GuidanceSnapshot Accept(GuidanceInput input)
    {
        var result = RecommendationGuidanceBuilder.Build(input);
        Check.That(result.Succeeded && result.Issue is null && result.Snapshot is not null, $"valid independent fixture accepted: {result.Issue}");
        return result.Snapshot!;
    }
    internal static byte[] Canonical(GuidanceSnapshot snapshot)
    {
        var node = JsonNode.Parse(CoreFixture.Json(snapshot).GetRawText())!.AsObject();
        node.Remove("contentDigest");
        return IndependentCanonical.Bytes(CoreFixture.Json(node));
    }
    internal static void Run()
    {
        var input = InputFixture.Create(); var initial = Accept(input);
        Check.Equal(initial.SchemaVersion, "synthetic-recommendation-guidance-v1", "literal schema");
        Check.Equal(initial.Status, "SyntheticUnverified", "literal synthetic authority status");
        Check.That(initial.Findings.Select(f => f.FindingId).SequenceEqual(new[] { CoreFixture.Hex('d'), CoreFixture.Hex('e') }), "literal stable identity ordering, no priority calculation");
        var first = initial.Findings[0]; var second = initial.Findings[1];
        Check.Equal(first.OriginalTitle, "Original fictional title", "original title retained");
        Check.Equal(first.PresentationTitle, "Current fictional title", "current title distinct");
        Check.Equal(first.BusinessContext, "Independent business context", "current context preserved");
        Check.Equal(first.CurrentState, "Rejected", "rejected finding retains guidance");
        Check.Equal(first.FindingRevision, 2L, "captured current revision");
        Check.Equal(first.Options.Length, 2, "all original alternatives retained");
        Check.That(first.Options.Select(o => o.OptionId).SequenceEqual(new[] { "OPT-A", "OPT-Z" }), "literal sorted alternatives");
        Check.That(first.Occurrences.Select(o => o.ObjectId).SequenceEqual(new[] { "SYN-OBJECT-A", "SYN-OBJECT-Z" }), "literal grouped object occurrence order");
        Check.That(first.Occurrences.Select(o => o.OriginalDigest).SequenceEqual(new[] { CoreFixture.Hex('9'), CoreFixture.Hex('8') }), "literal per-object originals");
        foreach (var finding in initial.Findings)
            foreach (var option in finding.Options)
            {
                var bytes = Encoding.UTF8.GetBytes("{\"findingId\":\"" + finding.FindingId + "\",\"optionId\":\"" + option.OptionId +
                    "\",\"runId\":\"11111111-2222-3333-4444-555555555555\",\"scope\":{\"customerId\":\"synthetic-customer\",\"environmentId\":\"synthetic-environment\",\"projectId\":\"synthetic-project\"}}");
                Check.Equal(option.ScopedOptionId, IndependentCanonical.Hash(bytes), "literal independent scoped identity bytes and SHA256");
                Check.Equal(option.Status, "Unverified", "finding dispositions never review advice");
            }
        Check.That(first.Options[0].ScopedOptionId != second.Options[0].ScopedOptionId, "repeated option ID across groups is separately scoped");
        Check.That(first.ValidationGuidance.SequenceEqual(new[] { "Validate first", "Validate second" }) &&
            first.GuidanceReferences.SequenceEqual(new[] { "fixture:reference-first", "fixture:reference-second" }), "ordered complete validation/reference literals");
        Check.That(first.Assumptions.SequenceEqual(new[] { "Fictional assumption" }) && first.Limitations.SequenceEqual(new[] { "Unverified fixture limitation" }), "exact assumptions/limitations");
        Check.Equal(first.Options[0].Text, "First existing fictional option", "option text exact");
        Check.Equal(first.Options[0].Prerequisites, "First prerequisite", "prerequisites exact");
        Check.Equal(first.Options[0].Risk, "First risk", "risk exact");
        Check.Equal(first.Options[0].RecoveryGuidance, "First recovery", "recovery exact");
        var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "canonical-golden.json"));
        Check.That(Canonical(initial).SequenceEqual(expected), "complete literal canonical golden bytes independently authored before execution");
        Check.Equal(initial.ContentDigest, IndependentCanonical.Hash(expected), "independent complete literal golden SHA256");
        Check.That(Canonical(initial).SequenceEqual(IndependentCanonical.Bytes(CoreFixture.Json(JsonNode.Parse(Encoding.UTF8.GetString(expected))!))), "literal golden is ordinal canonical JSON without trailing newline");
        Check.Group("RG-CORE-001 literal complete fields/grouping/scoped identities/canonical bytes/digest");
        var reversed = input with { Findings = input.Findings.Reverse().Select(f => f with { Options = f.Options.Reverse().ToImmutableArray(), Occurrences = f.Occurrences.Reverse().ToImmutableArray() }).ToImmutableArray() };
        var culture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Check.Equal(Accept(reversed).ContentDigest, initial.ContentDigest, "order/culture-independent complete snapshot"); }
        finally { CultureInfo.CurrentCulture = culture; }
        var retained = Canonical(initial); var retainedSource = initial.Source.FrozenVersions.GetRawText();
        using (var document = JsonDocument.Parse(input.Source.FrozenVersions.GetRawText()))
        {
            var owned = Accept(input with { Source = input.Source with { FrozenVersions = document.RootElement } });
            document.Dispose();
            Check.Equal(owned.Source.FrozenVersions.GetRawText(), retainedSource, "cloned JSON survives caller document disposal");
            Check.That(Canonical(owned).SequenceEqual(retained), "caller disposal cannot mutate canonical value");
        }
        var updatedFinding = input.Findings[1] with { CurrentState = "Confirmed", FindingRevision = 3, PresentationTitle = CoreFixture.Hostile, BusinessContext = CoreFixture.Hostile };
        var updated = Accept(input with { Source = input.Source with { ReviewSnapshotDigest = CoreFixture.Hex('7') }, Findings = input.Findings.SetItem(1, updatedFinding) });
        Check.That(updated.ContentDigest != initial.ContentDigest, "current presentation/state/review digest changes complete digest");
        Check.That(updated.Findings[0].Options.SequenceEqual(first.Options) && updated.Findings[0].Occurrences.SequenceEqual(first.Occurrences), "presentation and Confirm cannot change original option identity/provenance or review advice");
        Check.Equal(updated.Findings[0].PresentationTitle, CoreFixture.Hostile, "hostile HTML/link/script/formula remains exact text data");
        Check.That(Canonical(initial).SequenceEqual(retained), "later caller/review changes leave earlier returned bytes immutable");
        foreach (var changed in new[]
        {
            input with { Source = input.Source with { ReviewSnapshotDigest = CoreFixture.Hex('6') } },
            input with { Source = input.Source with { AnalysisContentDigest = CoreFixture.Hex('6') } },
            input with { Source = input.Source with { SavedCoverageDigest = CoreFixture.Hex('6') } },
            input with { Source = input.Source with { RunInputDigest = CoreFixture.Hex('6') } },
            input with { Findings = input.Findings.SetItem(1, input.Findings[1] with { RootCause = "Changed fictional root cause" }) },
            input with { Findings = input.Findings.SetItem(1, input.Findings[1] with { Options = input.Findings[1].Options.SetItem(0, input.Findings[1].Options[0] with { RecoveryGuidance = "Changed recovery" }) }) }
        }) Check.That(Accept(changed).ContentDigest != initial.ContentDigest, "every changed source/content field digest-bound");
        var empty = Accept(input with { Findings = ImmutableArray<GuidanceFindingInput>.Empty });
        Check.Equal(empty.Findings.Length, 0, "healthy/gap empty recommendation set never invents advice");
        Check.Equal(DraftSnapshotBuilder.Build(CoreFixture.Create()).Snapshot!.CanonicalContentDigest, "a48a9aefe6efcfa864812f6ba7b84211877a73df9e519704a114561dc31d07e9", "existing draft-v1 literal bytes remain unchanged");
        Check.Group("RG-CORE-002 independent ordering/immutability/current-vs-original/empty/draft compatibility");
        Deny(null, "missing input");
        Deny(input with { Source = null! }, "missing source");
        Deny(input with { Findings = default }, "uninitialized findings");
        foreach (var source in new[]
        {
            input.Source with { Scope = new("other", "synthetic-project", "synthetic-environment") },
            input.Source with { Scope = new("synthetic-customer", "other", "synthetic-environment") },
            input.Source with { Scope = new("synthetic-customer", "synthetic-project", "other") },
            input.Source with { RunId = Guid.Empty }, input.Source with { ReviewRunId = Guid.NewGuid() },
            input.Source with { RunRevision = -1 }, input.Source with { ReviewRunRevision = 12 },
            input.Source with { RunState = "Completed" }, input.Source with { ProfileId = "unknown-profile" },
            input.Source with { BaselineId = "unknown-baseline" }, input.Source with { RunInputDigest = "bad" },
            input.Source with { AnalysisFixtureDigest = CoreFixture.Hex('a') }, input.Source with { AnalysisContentDigest = "BAD" },
            input.Source with { SavedCoverageDigest = "bad" }, input.Source with { ReviewSnapshotDigest = "bad" },
            input.Source with { FrozenVersions = default }, input.Source with { CapabilityLock = default }, input.Source with { AnalysisLock = default },
            input.Source with { FrozenVersions = CoreFixture.Change(input.Source.FrozenVersions, n => n["profileVersion"] = "unknown") },
            input.Source with { FrozenVersions = CoreFixture.Change(input.Source.FrozenVersions, n => n["unexpected"] = true) },
            input.Source with { CapabilityLock = CoreFixture.Change(input.Source.CapabilityLock, n => n["unexpected"] = true) },
            input.Source with { AnalysisLock = CoreFixture.Change(input.Source.AnalysisLock, n => n["scope"]!["customerId"] = "other") },
            input.Source with { AnalysisLock = CoreFixture.Change(input.Source.AnalysisLock, n => n["unexpected"] = true) }
        }) Deny(input with { Source = source }, "malformed/cross-scope/run/revision/version/lock source");
        foreach (var finding in new[]
        {
            input.Findings[0] with { FindingId = "bad" }, input.Findings[0] with { RuleId = "" },
            input.Findings[0] with { RuleVersion = "unknown" }, input.Findings[0] with { CategoryId = "unknown" },
            input.Findings[0] with { Severity = "unknown" }, input.Findings[0] with { InitialState = "Confirmed" },
            input.Findings[0] with { CurrentState = "Validated" }, input.Findings[0] with { FindingRevision = -1 },
            input.Findings[0] with { OriginalTitle = "" }, input.Findings[0] with { PresentationTitle = "" },
            input.Findings[0] with { RootCause = "" }, input.Findings[0] with { Occurrences = default },
            input.Findings[0] with { Occurrences = ImmutableArray<GuidanceOccurrence>.Empty },
            input.Findings[0] with { Occurrences = input.Findings[0].Occurrences.Add(input.Findings[0].Occurrences[0]) },
            input.Findings[0] with { Options = default }, input.Findings[0] with { Options = ImmutableArray<GuidanceOptionInput>.Empty },
            input.Findings[0] with { Options = input.Findings[0].Options.Add(input.Findings[0].Options[0]) },
            input.Findings[0] with { ValidationGuidance = default }, input.Findings[0] with { GuidanceReferences = default },
            input.Findings[0] with { Assumptions = default }, input.Findings[0] with { Limitations = default }
        }) Deny(input with { Findings = input.Findings.SetItem(0, finding) }, "malformed current/original finding data");
        foreach (var option in new[] { new GuidanceOptionInput("", "Text", "Prereq", "Risk", "Recovery"), new("OPT-A", "", "Prereq", "Risk", "Recovery"),
            new("OPT-A", "Text", "", "Risk", "Recovery"), new("OPT-A", "Text", "Prereq", "", "Recovery"), new("OPT-A", "Text", "Prereq", "Risk", "") })
            Deny(input with { Findings = input.Findings.SetItem(0, input.Findings[0] with { Options = ImmutableArray.Create(option) }) }, "missing structured option field");
        Deny(input with { Findings = input.Findings.Add(input.Findings[0]) }, "duplicate grouped finding");
        Deny(input with { Findings = input.Findings.SetItem(1, input.Findings[1] with { Occurrences = input.Findings[0].Occurrences }) }, "cross-finding duplicate occurrence link");
        foreach (var profile in HistoricalGoldens.Values.Keys) Deny(input with { Source = input.Source with { ProfileId = profile } }, "all four legacy profiles remain opted out");
        Check.Group("RG-CORE-003 missing/unknown/cross-scope/run/revision/versions/locks/duplicate/current/original/option denial");
    }
    private static void Deny(GuidanceInput? input, string message)
    {
        var result = RecommendationGuidanceBuilder.Build(input);
        Check.That(!result.Succeeded && result.Snapshot is null && result.Issue is not null, message + " fails closed without fallback");
    }
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentRuns;
using DeterministicAnalysis;
using FindingReview;
using RecommendationGuidance;
using ReportDrafts;
using SyntheticFixPackages;

var checks = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) throw new InvalidOperationException("FAIL: " + name);
}
var directory = new DirectoryInfo(AppContext.BaseDirectory);
while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "independent-oracle.py"))) directory = directory.Parent;
var fixturePath = directory?.FullName ?? throw new InvalidOperationException("Independent fixtures missing.");
string Golden(string name) => File.ReadAllText(Path.Combine(fixturePath, name));
string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
JsonElement Json(JsonNode value) => JsonSerializer.SerializeToElement(value);
JsonObject Node(JsonElement value) => JsonNode.Parse(value.GetRawText())!.AsObject();
var profileId = "synthetic-review-maturity-fix-packages-equal-v1";
var templateDigest = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
Check(DemoFixPackageCatalog.ProfileId == profileId && DemoFixPackageCatalog.ApplicationVersion == "synthetic-fix-packages-app-v1" &&
    DemoFixPackageCatalog.TemplateVersion == "fictional-fix-templates-v1" && DemoFixPackageCatalog.TemplateDigest == templateDigest, "exact literal opt-in constants");
Check(DemoFixtureCatalog.Baselines.Count == 9 && DemoFixtureCatalog.Profiles.Count == 9 &&
    DemoFixtureCatalog.Profiles.Count(item => item.Id == profileId) == 1, "one profile and no baseline added");
Check(DemoFixtureCatalog.Profiles.Single(item => item.Id == profileId).Name == "Synthetic consultant review + fictional fix packages · equal weights", "literal profile label");
Check(DemoAnalysisCatalog.AnalysisProfileId(profileId) == "synthetic-analysis-equal-v1", "explicit underlying mapping");
Check(!DemoFixPackageCatalog.MatchesFrozenFixture(null), "null catalog source denied");

GuidanceInput NewInput()
{
    var input = GuidanceFixture.Create(); var versions = Node(input.Source.FrozenVersions);
    versions["applicationVersion"] = "synthetic-fix-packages-app-v1";
    versions["fixPackageTemplateDigest"] = templateDigest;
    return input with { Source = input.Source with { ProfileId = profileId, FrozenVersions = Json(versions) } };
}
var primitive = NewInput();
var oracle = JsonNode.Parse(Golden("golden-digests.json"))!;
foreach (var scenario in new[] { "normal", "empty", "multi", "hostile" })
{
    var input = primitive;
    if (scenario == "empty") input = input with { Findings = [] };
    if (scenario == "multi")
    {
        var first = input.Findings[0] with
        {
            Options = [.. input.Findings[0].Options, new("compare-fixture", "Compare fictional evidence.", "A separate fixture run.", "No closure claim.", "Retain originals.")],
            Occurrences = [.. input.Findings[0].Occurrences, new(new string('f', 64), "OBJECT-2", "SyntheticControl", "SyntheticOperations", GuidanceFixture.Digest, "fixture-evidence:OBJECT-2")]
        };
        var second = input.Findings[0] with { FindingId = new string('d', 64), Occurrences = [new(new string('e', 64), "OBJECT-3", "SyntheticControl", "SyntheticSecurity", GuidanceFixture.Digest, "fixture-evidence:OBJECT-3")] };
        input = input with { Findings = [second, first] };
    }
    if (scenario == "hostile")
    {
        const string text = "<script>alert('x')</script> \0\r\né e\u0301 😀 \" + & ` =SUM(A1)\nhttps://invalid.example/evil";
        input = input with { Findings = [input.Findings[0] with { OriginalTitle = text, PresentationTitle = text, BusinessContext = text, Options = [input.Findings[0].Options[0] with { Text = text }] }] };
    }
    var guidance = RecommendationGuidanceBuilder.Build(input);
    Check(guidance.Succeeded, scenario + " new source exact validator");
    var result = FixPackageBuilder.Build(guidance.Snapshot);
    Check(result.Succeeded, scenario + " real builder new source");
    var value = result.Snapshot!;
    Check(value.CanonicalJson == Golden(scenario + "-golden.json"), scenario + " independent literal whole canonical golden");
    Check(value.ContentDigest == oracle[scenario]!["contentDigest"]!.GetValue<string>() &&
        value.Guidance.ContentDigest == oracle[scenario]!["guidanceDigest"]!.GetValue<string>(), scenario + " independent complete digest chain");
    var displayed = JsonSerializer.SerializeToNode(value, DemoReportDraftProjection.JsonOptions)!.AsObject();
    displayed.Remove("canonicalJson"); displayed.Remove("contentDigest");
    Check(JsonNode.DeepEquals(displayed, JsonNode.Parse(Golden(scenario + "-golden.json"))), scenario + " every actual displayed field matches manual oracle");
    Check(value.TemplateDigest == templateDigest && value.Status == "Unverified" && value.Packages.All(p => p.Options.All(o => o.Artifacts.All(a => a.Status == "Unverified"))), scenario + " no review permission inferred");
}
// Original canonical fixtures are copied unchanged from pre-Cycle12 source and never regenerated.
var oldGuidance = RecommendationGuidanceBuilder.Build(GuidanceFixture.Create()).Snapshot!;
Check(Encoding.UTF8.GetString(RecommendationGuidanceBuilder.CanonicalPayload(oldGuidance)) == Golden("historical-guidance-golden.json") &&
    oldGuidance.ContentDigest == "d141e78843b8df1307b98f4d3f80724574c8c7177a6e87fb9c70035b789d6b46", "historical complete guidance bytes/digest unchanged");
var typedOld = new DemoRecommendationGuidanceDetail("Ready", null, oldGuidance);
var expectedOld = new { status = "Ready", reasonCode = (string?)null, snapshot = oldGuidance };
Check(JsonSerializer.Serialize(typedOld, DemoReportDraftProjection.JsonOptions) == JsonSerializer.Serialize(expectedOld, DemoReportDraftProjection.JsonOptions), "typed historical guidance exact response bytes/order");
Check(JsonSerializer.Serialize(new DemoRecommendationGuidanceDetail("Unavailable", "guidance_source_unavailable", null), DemoReportDraftProjection.JsonOptions) ==
    "{\"status\":\"Unavailable\",\"reasonCode\":\"guidance_source_unavailable\",\"snapshot\":null}", "typed historical unavailable literal bytes");
var oldDraftInput = DraftFixture.Create();
var oldDraft = DraftSnapshotBuilder.Build(oldDraftInput);
Check(oldDraft.Succeeded && DraftSnapshotBuilder.ValidateSnapshot(oldDraft.Snapshot), "historical draft unchanged source accepted");
var oldDraftContent = Node(oldDraftInput.Content);
oldDraftContent["healthyControls"] = new JsonArray(oldDraftContent["healthyControls"]!.AsArray().Reverse().Select(item => item!.DeepClone()).ToArray());
var oldDraftCapability = Node(oldDraftInput.Source.CapabilityLock);
oldDraftCapability["modules"] = new JsonArray(oldDraftCapability["modules"]!.AsArray().Reverse().Select(item => item!.DeepClone()).ToArray());
var oldDraftExpected = IndependentCanonical.Bytes(JsonSerializer.SerializeToElement(new
{
    schemaVersion = "synthetic-draft-report-v1",
    status = "SyntheticDraft",
    source = oldDraftInput.Source with { CapabilityLock = Json(oldDraftCapability) },
    content = Json(oldDraftContent)
}, DemoReportDraftProjection.JsonOptions));
Check(DraftSnapshotBuilder.CanonicalPayload(oldDraft.Snapshot!).SequenceEqual(oldDraftExpected), "complete historical draft bytes against literal input independent serializer");
var newDraftVersions = Node(oldDraftInput.Source.FrozenVersions);
newDraftVersions["applicationVersion"] = "synthetic-fix-packages-app-v1"; newDraftVersions["fixPackageTemplateDigest"] = templateDigest;
var newDraft = oldDraftInput with { Source = oldDraftInput.Source with { ProfileId = profileId, FrozenVersions = Json(newDraftVersions) } };
Check(DraftSnapshotBuilder.Build(newDraft).Succeeded, "draft exact new profile source branch");
foreach (var field in new[] { "applicationVersion", "fixPackageTemplateDigest" })
{
    var versions = Node(primitive.Source.FrozenVersions); versions[field] = "wrong";
    Check(!RecommendationGuidanceBuilder.Build(primitive with { Source = primitive.Source with { FrozenVersions = Json(versions) } }).Succeeded, "new guidance refuses wrong " + field);
    versions = Node(newDraft.Source.FrozenVersions); versions[field] = "wrong";
    Check(!DraftSnapshotBuilder.Build(newDraft with { Source = newDraft.Source with { FrozenVersions = Json(versions) } }).Succeeded, "new draft refuses wrong " + field);
}
foreach (var remove in new[] { "fixPackageTemplateDigest", "analysisFixtureDigest", "maturityFixtureDigest" })
{
    var versions = Node(primitive.Source.FrozenVersions); versions.Remove(remove);
    Check(!RecommendationGuidanceBuilder.Build(primitive with { Source = primitive.Source with { FrozenVersions = Json(versions) } }).Succeeded, "guidance missing exact frozen member " + remove);
    versions = Node(newDraft.Source.FrozenVersions); versions.Remove(remove);
    Check(!DraftSnapshotBuilder.Build(newDraft with { Source = newDraft.Source with { FrozenVersions = Json(versions) } }).Succeeded, "draft missing exact frozen member " + remove);
}
var extra = Node(primitive.Source.FrozenVersions); extra["extra"] = "value";
Check(!RecommendationGuidanceBuilder.Build(primitive with { Source = primitive.Source with { FrozenVersions = Json(extra) } }).Succeeded, "guidance refuses thirteenth field");
Check(!DraftSnapshotBuilder.Build(newDraft with { Source = newDraft.Source with { FrozenVersions = Json(extra) } }).Succeeded, "draft refuses thirteenth field");
var operations = Node(primitive.Source.AnalysisLock); operations["profileId"] = "synthetic-analysis-operations-v1";
Check(!RecommendationGuidanceBuilder.Build(primitive with { Source = primitive.Source with { AnalysisLock = Json(operations) } }).Succeeded, "new guidance explicit equal mapping closed");
Check(!DraftSnapshotBuilder.Build(newDraft with { Source = newDraft.Source with { AnalysisLock = Json(operations) } }).Succeeded, "new draft explicit equal mapping closed");
foreach (var legacy in new[] { "synthetic-review-maturity-equal-v1", "synthetic-review-maturity-operations-v1" })
{
    Check(!RecommendationGuidanceBuilder.Build(primitive with { Source = primitive.Source with { ProfileId = legacy } }).Succeeded, "old guidance rejects new twelve-field envelope " + legacy);
    Check(!DraftSnapshotBuilder.Build(newDraft with { Source = newDraft.Source with { ProfileId = legacy } }).Succeeded, "old draft rejects new twelve-field envelope " + legacy);
}

foreach (var (preset, packages, occurrences) in new[]
{
    ("synthetic-analysis-findings-v1", 5, 10), ("synthetic-analysis-mixed-v1", 5, 5),
    ("synthetic-analysis-healthy-v1", 0, 0), ("synthetic-analysis-gaps-v1", 0, 0)
})
{
    var run = SavedFixture.Run(preset, profileId);
    var response = SyntheticDemoAnalysisAdapter.Project(run);
    var review = SavedFixture.Review(run);
    var captured = DemoRecommendationGuidanceProjection.Detail(run, response, review);
    Check(DemoFixPackageCatalog.MatchesFrozenFixture(run), preset + " exact frozen fixture");
    Check(run.FrozenInputs.FixPackageTemplateDigest == templateDigest && run.FrozenInputs.AiPreviewFixtureDigest is null &&
        run.FrozenInputs.AiPolicyVersion == "synthetic-ai-disabled-v1" &&
        JsonSerializer.SerializeToElement(run.FrozenInputs, DemoReportDraftProjection.JsonOptions).EnumerateObject().Count() == 12, preset + " twelve exact frozen fields, AI disabled");
    var detail = DemoFixPackageProjection.Detail(run, captured, review)!;
    Check(detail.Status == "Ready" && detail.ReasonCode is null && detail.Snapshot is not null, preset + " actual saved-source composition ready");
    var value = detail.Snapshot!;
    Check(detail.SchemaVersion == "synthetic-fix-package-demo-v1" && detail.RunId == run.RunId && detail.RunRevision == 13 &&
        detail.RunInputDigest == run.InputDigest && detail.BaselineId == preset && detail.ProfileId == profileId, preset + " all nine detail fields");
    Check(JsonSerializer.SerializeToElement(detail, DemoReportDraftProjection.JsonOptions).EnumerateObject().Count() == 9, preset + " closed detail shape");
    Check(value.Packages.Length == packages && value.Guidance.Findings.Sum(f => f.Occurrences.Length) == occurrences &&
        value.Packages.Sum(p => p.Options.Length) == packages * 2 && value.Packages.Sum(p => p.Options.Sum(o => o.Artifacts.Length)) == packages * 6, preset + " independently specified grouped/options/artifact counts");
    Check(JsonSerializer.Serialize(value.Guidance, DemoReportDraftProjection.JsonOptions) == JsonSerializer.Serialize(captured.Snapshot, DemoReportDraftProjection.JsonOptions), preset + " exact complete captured guidance retained");
    Check(value.Guidance.Source.ReviewSnapshotDigest == review.Snapshot!.SnapshotDigest, preset + " same current captured review hash");
    Check(Hash(value.CanonicalJson) == value.ContentDigest && Encoding.UTF8.GetString(FixPackageBuilder.CanonicalPayload(value)) == value.CanonicalJson, preset + " complete actual-field/cached canonical integrity");
    VerifyArtifacts(value, preset);
    Check(DemoFixPackageProjection.Detail(run, captured, review)!.Snapshot!.CanonicalJson == value.CanonicalJson, preset + " repeated read deterministic");
    Check(DemoFixPackageProjection.Detail(run with { Results = run.Results.Reverse().ToArray() }, captured, review)!.Snapshot!.CanonicalJson == value.CanonicalJson, preset + " result read ordering retained");
    if (packages == 0) Check(value.Warnings.Contains("No findings were supplied; no fix packages or actions are available."), preset + " empty does not assert healthy/remediation");
    void Denied(SyntheticRunSnapshot changed, string name, DemoRecommendationGuidanceDetail? supplied = null, DemoReviewContext? context = null)
    {
        var denied = DemoFixPackageProjection.Detail(changed, supplied ?? captured, context ?? review);
        Check(denied is { Status: "Unavailable", Snapshot: null, ReasonCode: not null } && denied.ReasonCode.StartsWith("fix_packages_", StringComparison.Ordinal), preset + " payload-free denial " + name);
    }
    foreach (var state in Enum.GetValues<SyntheticRunState>().Where(state => state != SyntheticRunState.Scoring)) Denied(run with { State = state }, "state " + state);
    Denied(run with { RunId = Guid.Empty }, "empty run"); Denied(run with { Revision = 0 }, "revision");
    Denied(run with { InputDigest = new string('0', 64) }, "input digest"); Denied(run with { CancelRequested = true }, "cancel");
    Denied(run with { Scope = run.Scope with { CustomerId = "other" } }, "customer");
    Denied(run with { Scope = run.Scope with { ProjectId = "other" } }, "project");
    Denied(run with { Scope = run.Scope with { EnvironmentId = "other" } }, "environment");
    Denied(run with { Lease = new("worker", Guid.NewGuid(), run.ObservedAtDatabaseUtc) }, "lease");
    Denied(run with { CheckpointSequence = 0 }, "checkpoint");
    Denied(run with { InFlightKeys = [run.Plan.ExpectedKeys[0]] }, "in flight");
    Denied(run with { CoverageSummary = null }, "summary missing");
    Denied(run with { Results = [] }, "coverage missing");
    Denied(run with { Results = [.. run.Results, run.Results[0]] }, "duplicate terminal result");
    Denied(run with { Results = [run.Results[0] with { EvidenceReference = "foreign" }, .. run.Results.Skip(1)] }, "result evidence");
    Denied(run with { Results = [null!, .. run.Results.Skip(1)] }, "null result");
    Denied(run with { Results = null! }, "null result list"); Denied(run with { FrozenInputs = null! }, "null versions");
    Denied(run with { Plan = null! }, "null plan"); Denied(run with { InFlightKeys = null! }, "null in flight");
    Denied(run with { CoverageSummary = run.CoverageSummary! with { Counts = [] } }, "summary counts");
    Denied(run with { CoverageSummary = run.CoverageSummary! with { ExecutableCoverage = new(0, 1) } }, "summary ratio");
    Denied(run with { CoverageSummary = run.CoverageSummary! with { Kind = run.CoverageSummary.Kind == CoverageCompletionKind.Complete ? CoverageCompletionKind.CompleteWithGaps : CoverageCompletionKind.Complete } }, "summary kind");
    foreach (var property in typeof(SyntheticRunInputVersions).GetProperties())
    {
        var versions = JsonSerializer.SerializeToNode(run.FrozenInputs)!.AsObject();
        versions[property.Name] = property.Name.EndsWith("Digest", StringComparison.Ordinal) ? new string('0', 64) : "wrong";
        var altered = versions.Deserialize<SyntheticRunInputVersions>()!;
        Denied(run with { FrozenInputs = altered }, "version " + property.Name);
        Denied(run with { FrozenInputs = altered, InputDigest = SyntheticDurableRunEngine.ComputeInputDigest(run.Plan, altered, run.BaselineCatalogId, run.ProfileCatalogId) }, "rehashed version " + property.Name);
    }
    var alteredPlan = run.Plan with { CapabilityLock = run.Plan.CapabilityLock with { RuleCatalogVersion = "wrong" } };
    Denied(run with { Plan = alteredPlan, InputDigest = SyntheticDurableRunEngine.ComputeInputDigest(alteredPlan, run.FrozenInputs, run.BaselineCatalogId, run.ProfileCatalogId) }, "rehashed capability");
    Denied(run with { Plan = run.Plan with { ExpectedKeys = [] } }, "plan keys");
    Denied(run with { Plan = run.Plan with { Objects = run.Plan.Objects.Reverse().ToArray() } }, "plan ordering");
    Check(DemoFixPackageProjection.Detail(run, null, review) is { Status: "Unavailable", Snapshot: null }, preset + " missing captured guidance");
    Check(DemoFixPackageProjection.Detail(run, captured, null) is { Status: "Unavailable", Snapshot: null }, preset + " missing captured review");
    Denied(run, "guidance denied", new("Unavailable", "guidance_source_unavailable", null));
    Denied(run, "guidance contradictory reason", new("Ready", "wrong", captured.Snapshot));
    Denied(run, "review reason", context: new("wrong", review.Snapshot));
    Denied(run, "review digest altered", context: new(null, review.Snapshot! with { SnapshotDigest = new string('a', 64) }));
    Denied(run, "guidance digest altered", new("Ready", null, captured.Snapshot! with { ContentDigest = new string('a', 64) }));
    Denied(run, "guidance default findings", new("Ready", null, captured.Snapshot! with { Findings = default }));
    var changedSource = captured.Snapshot!.Source with { ReviewSnapshotDigest = new string('b', 64) };
    var forged = Rehash(captured.Snapshot with { Source = changedSource });
    Denied(run, "fully rehashed wrong review source", new("Ready", null, forged));
    if (packages > 0)
    {
        foreach (var transform in new Func<GuidanceFinding, GuidanceFinding>[]
        {
            f => f with { FindingRevision = f.FindingRevision + 1 }, f => f with { PresentationTitle = "wrong" },
            f => f with { BusinessContext = "wrong" }, f => f with { CurrentState = "Rejected" },
            f => f with { OriginalTitle = "wrong" }, f => f with { RootCause = "wrong" },
            f => f with { Options = [f.Options[0] with { Text = "changed" }, .. f.Options.Skip(1)] },
            f => f with { Occurrences = [f.Occurrences[0] with { EvidenceReference = "changed" }, .. f.Occurrences.Skip(1)] }
        })
        {
            forged = Rehash(captured.Snapshot with { Findings = [transform(captured.Snapshot.Findings[0]), .. captured.Snapshot.Findings.Skip(1)] });
            Denied(run, "rehashed current/original finding mutation", new("Ready", null, forged));
        }
        foreach (var disposition in new[] { SyntheticFindingState.Confirmed, SyntheticFindingState.Rejected, SyntheticFindingState.Deferred })
        {
            var updatedReview = SavedFixture.Change(review, disposition, "Current café 漢字 😀 <script>bad()</script>\0\r\n", "Context & =SUM(A1)");
            var updated = DemoRecommendationGuidanceProjection.Detail(run, response, updatedReview);
            var current = DemoFixPackageProjection.Detail(run, updated, updatedReview)!;
            Check(current.Status == "Ready" && current.RunRevision == detail.RunRevision && current.Snapshot!.ContentDigest != value.ContentDigest &&
                current.Snapshot.Guidance.Source.ReviewSnapshotDigest != value.Guidance.Source.ReviewSnapshotDigest, preset + " current review changes digest independently of run " + disposition);
            Check(current.Snapshot!.Packages.SequenceEqual(value.Packages, new PackageValueComparer()), preset + " disposition does not change artifact identities/text/status " + disposition);
            Check(current.Snapshot.Guidance.Findings.Any(f => f.CurrentState == disposition.ToString() && f.FindingRevision == 2 && f.PresentationTitle.EndsWith("\0\r\n", StringComparison.Ordinal)), preset + " current finding values/control text retained " + disposition);
            Denied(run, "old guidance vs new same-run review", context: updatedReview);
            Denied(run, "new guidance vs old same-run review", updated);
        }
    }
    // A new run cannot reuse an old capture, even when fixed inputs and coverage are identical.
    Denied(run with { RunId = Guid.Parse("11111111-2222-3333-4444-555555555555") }, "cross-run captured source");
    Denied(run with { Revision = 14 }, "cross-revision captured source");
}
foreach (var baseline in DemoFixtureCatalog.Baselines)
{
    var allowed = baseline.Id is "synthetic-analysis-healthy-v1" or "synthetic-analysis-findings-v1" or "synthetic-analysis-mixed-v1" or "synthetic-analysis-gaps-v1";
    Check(DemoAnalysisCatalog.Compatible(baseline.Id, profileId) == allowed, "literal four-preset compatibility " + baseline.Id);
    if (!allowed)
    {
        try { DemoFixtureCatalog.CreateStartRequest(baseline.Id, profileId, "wrong"); Check(false, "incompatible start accepted"); }
        catch (ArgumentException) { Check(true, "incompatible start denied"); }
    }
}
foreach (var profile in DemoFixtureCatalog.Profiles.Where(item => item.Id != profileId))
{
    Check(profile.Versions.FixPackageTemplateDigest is null, "historical catalog optional digest absent " + profile.Id);
    var baseline = DemoFixtureCatalog.Baselines.First(item => DemoAnalysisCatalog.Compatible(item.Id, profile.Id));
    var run = SavedFixture.Run(baseline.Id, profile.Id);
    Check(!JsonSerializer.Serialize(run.FrozenInputs).Contains("FixPackageTemplateDigest", StringComparison.Ordinal), "historical saved envelope omits field " + profile.Id);
    Check(DemoFixPackageProjection.Detail(run, null, null) is null, "historical packages null " + profile.Id);
    Check(!DemoAnalysisCatalog.MatchesFrozenFixture(run with { FrozenInputs = run.FrozenInputs with { FixPackageTemplateDigest = templateDigest } }), "historical injected lock denied " + profile.Id);
    if (profile.Id is "profile-standard" or "profile-comparison")
        Check(JsonSerializer.Serialize(profile.Versions) == Golden("historical-" + (profile.Id == "profile-standard" ? "standard" : "comparison") + "-golden.json"), "historical complete original version bytes " + profile.Id);
    if (profile.Id is "synthetic-review-maturity-equal-v1" or "synthetic-review-maturity-operations-v1")
    {
        var context = SavedFixture.Review(run); var guidance = DemoRecommendationGuidanceProjection.Detail(run, SyntheticDemoAnalysisAdapter.Project(run), context);
        Check(guidance is { Status: "Ready", Snapshot: not null } && guidance.Snapshot.Source.FrozenVersions.EnumerateObject().Count() == 11, "actual historical guidance eleven-field source " + profile.Id);
    }
}
Console.WriteLine($"PASS: {checks} substantive local fictional fix-package catalog/source/projection assertions.");

GuidanceSnapshot Rehash(GuidanceSnapshot value) => value with { ContentDigest = Convert.ToHexStringLower(SHA256.HashData(RecommendationGuidanceBuilder.CanonicalPayload(value))) };
void VerifyArtifacts(FixPackageSnapshot value, string label)
{
    var templates = new[]
    {
        new FixTemplate("fictional-config-v1", "Configuration", "{\n  \"fixtureOnly\": true,\n  \"reviewRequired\": true\n}"),
        new FixTemplate("fictional-script-v1", "Script", "# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),
        new FixTemplate("fictional-sql-v1", "Sql", "-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")
    };
    Check(value.Templates.SequenceEqual(templates) && value.TemplateDigest == templateDigest, label + " exact independent fixed templates");
    foreach (var package in value.Packages)
    {
        var expectedId = IndependentCanonical.Hash(new { findingId = package.FindingId, runId = value.Guidance.Source.RunId.ToString("D"), scope = value.Guidance.Source.Scope });
        Check(package.PackageId == expectedId, label + " independent package ID recipe");
        foreach (var option in package.Options)
        {
            var guidance = value.Guidance.Findings.Single(f => f.FindingId == package.FindingId).Options.Single(o => o.ScopedOptionId == option.ScopedOptionId);
            Check(option.ScopedOptionId == IndependentCanonical.Hash(new { findingId = package.FindingId, optionId = guidance.OptionId, runId = value.Guidance.Source.RunId.ToString("D"), scope = value.Guidance.Source.Scope }), label + " independent scoped option ID");
            for (var i = 0; i < 3; i++)
            {
                var expectedArtifact = new FixArtifact(IndependentCanonical.Hash(new { packageId = expectedId, scopedOptionId = option.ScopedOptionId, templateId = templates[i].TemplateId, templateVersion = "fictional-fix-templates-v1" }), templates[i].TemplateId, templates[i].Kind, "Unverified", templates[i].Text);
                Check(option.Artifacts[i] == expectedArtifact, label + " complete independent artifact identity/kind/text/status");
            }
        }
    }
}

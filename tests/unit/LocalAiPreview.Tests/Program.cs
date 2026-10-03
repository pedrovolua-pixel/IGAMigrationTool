using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentRuns;

var checks = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) throw new InvalidOperationException("FAIL: " + name);
}
string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
var basePath = new DirectoryInfo(AppContext.BaseDirectory);
while (basePath is not null && !File.Exists(Path.Combine(basePath.FullName, "oracle.py"))) basePath = basePath.Parent;
var fixturePath = basePath?.FullName ?? throw new InvalidOperationException("Independent test oracle files missing.");
string Golden(string name) => File.ReadAllText(Path.Combine(fixturePath, name));
var manifest = JsonNode.Parse(Golden("golden-manifest.json"))!;
var baseline = DemoAiPreviewCatalog.CreateBaseline();
Check(baseline.Id == "baseline-ai-configuration-v1", "literal dedicated baseline");
Check(DemoFixtureCatalog.Baselines.Count(item => item.Id == baseline.Id) == 1, "unique baseline catalog");
Check(DemoFixtureCatalog.Profiles.Count(item => DemoAiPreviewCatalog.IsProfile(item.Id)) == 2, "exactly two opt-in profiles");
Check(Hash(DemoAiPreviewCatalog.PacketTemplate) == manifest["packetTemplateDigest"]!.GetValue<string>(), "independent complete packet template digest");
Check(DemoAiPreviewCatalog.PacketTemplateDigest == manifest["packetTemplateDigest"]!.GetValue<string>(), "catalog packet template digest");
Check(DemoFixtureCatalog.ScriptDigest(baseline.Id) == manifest["scriptDigest"]!.GetValue<string>(), "literal scripted outcomes digest");
foreach (var template in new[] { DemoAiPreviewCatalog.PacketTemplate, DemoAiPreviewCatalog.ResponseTemplate(DemoAiPreviewCatalog.ProfileId), DemoAiPreviewCatalog.ResponseTemplate(DemoAiPreviewCatalog.EmptyProfileId) })
{
    Check(!template.Contains('\r') && !template.StartsWith('\uFEFF') && template.EndsWith('\n'), "exact UTF8 LF template framing");
    Check(Hash(template + " ") != Hash(template), "complete template suffix bound");
}
foreach (var b in DemoFixtureCatalog.Baselines)
    foreach (var p in DemoFixtureCatalog.Profiles)
    {
        var isAi = b.Id == "baseline-ai-configuration-v1" || p.Id is "profile-ai-preview-v1" or "profile-ai-preview-empty-v1";
        var expected = isAi ? b.Id == "baseline-ai-configuration-v1" && p.Id is "profile-ai-preview-v1" or "profile-ai-preview-empty-v1"
            : DemoAnalysisCatalog.IsAnalysisBaseline(b.Id) == DemoAnalysisCatalog.IsAnalysisProfile(p.Id);
        Check(DemoAnalysisCatalog.Compatible(b.Id, p.Id) == expected, "closed compatibility " + b.Id + "/" + p.Id);
        if (!expected)
        {
            try { DemoFixtureCatalog.CreateStartRequest(b.Id, p.Id, "unit-incompatible"); Check(false, "incompatible request accepted"); }
            catch (ArgumentException) { Check(true, "incompatible request refused"); }
        }
    }
SyntheticRunSnapshot Snapshot(string profile)
{
    var request = DemoFixtureCatalog.CreateStartRequest(baseline.Id, profile, "unit-offline-preview");
    var plan = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, request.Scope).Plan!;
    var results = baseline.ScriptedResults;
    var summary = new SyntheticCoverageStageSummary(CoverageCompletionKind.Complete,
        Array.AsReadOnly(Enumerable.Range(0, 10).Select(index => new CoverageStateCount((CoverageState)index, index == 0 ? 2 : 0)).ToArray()),
        new(2, 2), Array.AsReadOnly(Array.Empty<CoverageLimitation>()));
    var clock = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    return new(Guid.Parse("12345678-1234-5678-9abc-123456789abc"), request.Scope, request.BaselineCatalogId,
        request.ProfileCatalogId, SyntheticRunState.Scoring, 7, false, 1,
        SyntheticDurableRunEngine.ComputeInputDigest(plan, request.Versions, baseline.Id, profile), request.Versions, plan,
        results, [], null, summary, clock, clock, [], clock);
}
void Denied(SyntheticRunSnapshot run, string label)
{
    var detail = DemoAiPreviewProjection.Detail(run);
    Check(detail is { Status: "Unavailable", Snapshot: null, ReasonCode: "ai_preview_source_unavailable" }, label + " payload-free unavailable");
    Check(detail!.FixtureDigest == DemoAiPreviewCatalog.FixtureDigest(run.ProfileCatalogId), label + " trusted catalog fixture binding");
}
foreach (var (profile, name) in new[] { (DemoAiPreviewCatalog.ProfileId, "normal"), (DemoAiPreviewCatalog.EmptyProfileId, "empty") })
{
    var run = Snapshot(profile);
    var expected = manifest["profiles"]![profile]!;
    Check(JsonSerializer.Serialize(run.Plan) == Golden("golden-plan.json"), name + " complete independent plan bytes");
    Check(JsonSerializer.Serialize(run.FrozenInputs) == Golden("golden-" + name + "-versions.json"), name + " complete independent frozen version bytes");
    Check(JsonSerializer.Serialize(run.CoverageSummary) == Golden("golden-summary.json"), name + " complete independent summary bytes");
    Check(run.InputDigest == expected["inputDigest"]!.GetValue<string>(), name + " independent framed input digest");
    Check(DemoAiPreviewCatalog.FixtureDigest(profile) == expected["fixtureDigest"]!.GetValue<string>(), name + " independent complete fixture recipe");
    Check(Hash(DemoAiPreviewCatalog.ResponseTemplate(profile)) == expected["responseTemplateDigest"]!.GetValue<string>(), name + " complete response template digest");
    Check(DemoAiPreviewCatalog.MatchesFrozenFixture(run), name + " expected exact source accepted");
    foreach (var state in new[] { SyntheticRunState.Planned, SyntheticRunState.Running })
    {
        var pending = run with { State = state, Results = [], CoverageSummary = null, CheckpointSequence = 0 };
        Check(DemoAnalysisCatalog.MatchesFrozenFixture(pending), name + " pending source accepted by worker gate");
        Denied(pending, name + " pending " + state);
    }
    var detail = DemoAiPreviewProjection.Detail(run)!;
    Check(detail.Status == "Ready" && detail.ReasonCode is null && detail.Snapshot is not null, name + " real builder composition ready");
    Check(detail.SchemaVersion == "synthetic-ai-demo-preview-v1" && detail.RunId == run.RunId && detail.RunRevision == 7 &&
        detail.RunInputDigest == run.InputDigest && detail.BaselineId == baseline.Id && detail.ProfileId == profile &&
        detail.FixtureDigest == expected["fixtureDigest"]!.GetValue<string>(), name + " all detail source fields");
    var snapshot = detail.Snapshot!;
    Check(snapshot.CanonicalJson == Golden("golden-" + name + "-preview.json"), name + " complete independent canonical preview bytes");
    Check(snapshot.ContentDigest == expected["previewDigest"]!.GetValue<string>(), name + " independent full preview digest");
    Check(snapshot.PacketDigest == expected["packetDigest"]!.GetValue<string>(), name + " actual packet digest");
    Check(snapshot.ProposalDigest == expected["proposalDigest"]!.GetValue<string>(), name + " actual proposal digest");
    Check(snapshot.Status == "Proposed" && snapshot.Source.RunId == run.RunId && snapshot.Source.ProfileDigest == run.InputDigest &&
        snapshot.Source.BaselineDigest == DemoAiPreviewCatalog.PacketTemplateDigest, name + " untrusted source binding");
    // Compare every typed field against separately authored canonical data, not a projection/helper round-trip.
    var typed = JsonSerializer.SerializeToNode(snapshot, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
    typed.Remove("canonicalJson"); typed.Remove("contentDigest");
    Check(JsonNode.DeepEquals(typed, JsonNode.Parse(Golden("golden-" + name + "-preview.json"))), name + " all typed displayed fields equal independent oracle");
    Check(snapshot.Proposals.Length == (name == "normal" ? 1 : 0), name + " exact proposal count");
    if (name == "normal")
    {
        Check(snapshot.Proposals[0].ConflictingEvidenceIds.Length == 2 && snapshot.Proposals[0].MissingContext.Length == 2, "conflict unresolved and context intact");
        Check(snapshot.Proposals[0].Suggestions[0].Text == "Review these fictional settings; <script>window.fixtureExecuted=true</script> is data only.", "hostile text preserved inertly");
        Check(snapshot.Proposals[0].Assumptions[0].Text == "Fictional context: café, 漢字, 😀.", "Unicode full field retained");
    }
    for (var index = 0; index < 8; index++)
        Check(DemoAiPreviewProjection.Detail(run)!.Snapshot!.CanonicalJson == snapshot.CanonicalJson, name + " deterministic repeated read " + index);
    var changedRun = run with { RunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), Revision = 9 };
    var changed = DemoAiPreviewProjection.Detail(changedRun)!;
    Check(changed.Status == "Ready" && changed.Snapshot!.ContentDigest != snapshot.ContentDigest && changed.Snapshot.Source.RunId == changedRun.RunId, name + " actual run rebinding");
    Denied(run with { RunId = Guid.Empty }, name + " empty run");
    Denied(run with { Revision = 0 }, name + " invalid revision");
    Denied(run with { Scope = run.Scope with { CustomerId = "other" } }, name + " wrong customer");
    Denied(run with { Scope = run.Scope with { ProjectId = "other" } }, name + " wrong project");
    Denied(run with { Scope = run.Scope with { EnvironmentId = "other" } }, name + " wrong environment");
    Denied(run with { BaselineCatalogId = "baseline-complete" }, name + " wrong baseline");
    Denied(run with { InputDigest = new string('0', 64) }, name + " incorrect input digest");
    Denied(run with { InputDigest = run.InputDigest.ToUpperInvariant() }, name + " uppercase digest");
    Denied(run with { InputDigest = "bad" }, name + " short digest");
    Denied(run with { CancelRequested = true }, name + " cancellation");
    Denied(run with { State = SyntheticRunState.Failed }, name + " failed state");
    Denied(run with { State = SyntheticRunState.Cancelled }, name + " cancelled state");
    Denied(run with { Lease = new("fictional-worker", Guid.NewGuid(), DateTimeOffset.UtcNow) }, name + " outstanding lease");
    Denied(run with { InFlightKeys = [run.Plan.ExpectedKeys[0]] }, name + " outstanding work");
    Denied(run with { CheckpointSequence = 0 }, name + " missing checkpoint");
    Denied(run with { CoverageSummary = null }, name + " missing summary");
    Denied(run with { CoverageSummary = run.CoverageSummary! with { Kind = CoverageCompletionKind.CompleteWithGaps } }, name + " changed summary classification");
    Denied(run with { CoverageSummary = run.CoverageSummary! with { ExecutableCoverage = new(1, 2) } }, name + " changed executable measure");
    Denied(run with { Results = [run.Results[0]] }, name + " missing result");
    Denied(run with { Results = [run.Results[0], run.Results[0]] }, name + " duplicate result");
    Denied(run with { Results = [run.Results[0] with { State = CoverageState.Finding }, run.Results[1]] }, name + " changed scripted result");
    Denied(run with { Results = [run.Results[0] with { EvidenceReference = "changed" }, run.Results[1]] }, name + " changed result evidence");
    Denied(run with { Plan = null! }, name + " missing plan");
    Denied(run with { FrozenInputs = null! }, name + " missing versions");
    Denied(run with { Results = null! }, name + " missing results collection");
    Denied(run with { Results = [null!, run.Results[1]] }, name + " invalid null result");
    Denied(run with { CoverageSummary = run.CoverageSummary! with { Counts = [new(CoverageState.Pass, 1)] } }, name + " changed summary counts");
    Denied(run with { Results = [run.Results[0], run.Results[1], run.Results[0] with { Key = new("unknown", "configuration") }] }, name + " unexpected result");
    Denied(run with { Results = [run.Results[0] with { State = CoverageState.Error, ReasonCode = "fixture-error", ResponsibleStage = "synthetic-worker" }, run.Results[1]] }, name + " explained error outcome");
    Denied(run with { Results = [run.Results[0] with { ResponsibleStage = "unexpected" }, run.Results[1]] }, name + " changed scripted stage");
    Denied(run with { Results = [run.Results[0] with { ReasonCode = "unexpected" }, run.Results[1]] }, name + " changed scripted reason");
    Check(DemoAiPreviewProjection.Detail(run with { Results = run.Results.Reverse().ToArray() })!.Snapshot!.CanonicalJson == snapshot.CanonicalJson, name + " result read order independent");
    Denied(run with { Plan = run.Plan with { BaselineId = "changed" } }, name + " plan baseline");
    Denied(run with { Plan = run.Plan with { ExpectedKeys = [run.Plan.ExpectedKeys[0]] } }, name + " partial expected plan");
    Denied(run with { Plan = run.Plan with { CapabilityLock = run.Plan.CapabilityLock with { LockDigest = new string('0', 64) } } }, name + " changed capability digest");
    Denied(run with { Plan = run.Plan with { CapabilityLock = run.Plan.CapabilityLock with { QueryPackVersion = "changed" } } }, name + " changed capability version");
    Denied(run with { Plan = run.Plan with { Objects = [run.Plan.Objects[0] with { NativeType = "changed" }, run.Plan.Objects[1]] } }, name + " changed object metadata");
    var alteredPlan = run.Plan with { CapabilityLock = run.Plan.CapabilityLock with { RuleCatalogVersion = "changed" } };
    Denied(run with { Plan = alteredPlan, InputDigest = SyntheticDurableRunEngine.ComputeInputDigest(alteredPlan, run.FrozenInputs, baseline.Id, profile) }, name + " internally relocked altered capability");
    Denied(run with { Plan = run.Plan with { Scope = run.Scope with { CustomerId = "other" } } }, name + " plan scope");
    Denied(run with { Plan = run.Plan with { Objects = run.Plan.Objects.Reverse().ToArray() } }, name + " changed plan object order");
    Denied(run with { FrozenInputs = run.FrozenInputs with { Phase1BLocks = DemoPhase1BAiFixture.Locks(run.RunId, new string('a', 64)) } }, name + " additive Phase1B locks denied on historical profile");
    foreach (var property in typeof(SyntheticRunInputVersions).GetProperties().Where(p => p.Name != nameof(SyntheticRunInputVersions.Phase1BLocks)))
    {
        var node = JsonSerializer.SerializeToNode(run.FrozenInputs)!.AsObject();
        node[property.Name] = property.Name.EndsWith("Digest", StringComparison.Ordinal) ? new string('0', 64) : "changed";
        var altered = node.Deserialize<SyntheticRunInputVersions>()!;
        Denied(run with { FrozenInputs = altered }, name + " changed version " + property.Name);
        var relocked = run with { FrozenInputs = altered, InputDigest = SyntheticDurableRunEngine.ComputeInputDigest(run.Plan, altered, run.BaselineCatalogId, run.ProfileCatalogId) };
        Denied(relocked, name + " self-consistent changed lock " + property.Name);
    }
    foreach (var optional in new[] { "AiPreviewFixtureDigest", "ScriptedResultsDigest" })
    {
        var node = JsonSerializer.SerializeToNode(run.FrozenInputs)!.AsObject(); node[optional] = null;
        Denied(run with { FrozenInputs = node.Deserialize<SyntheticRunInputVersions>()! }, name + " missing " + optional);
    }
}
foreach (var (profile, name) in new[] { ("profile-standard", "standard"), ("profile-comparison", "comparison") })
{
    var old = DemoFixtureCatalog.Profiles.Single(item => item.Id == profile);
    Check(JsonSerializer.Serialize(old.Versions) == Golden("golden-historical-" + name + ".json"), "unchanged original " + name + " serialized envelope");
}
foreach (var profile in DemoFixtureCatalog.Profiles.Where(item => !DemoAiPreviewCatalog.IsProfile(item.Id)))
{
    Check(profile.Versions.AiPreviewFixtureDigest is null, "historical catalog omits AI lock " + profile.Id);
    var compatible = DemoFixtureCatalog.Baselines.First(item => DemoAnalysisCatalog.Compatible(item.Id, profile.Id));
    var request = DemoFixtureCatalog.CreateStartRequest(compatible.Id, profile.Id, "unit-history");
    var plan = SyntheticBaselineInventoryPlanner.Plan(request.Capability, request.Baseline, request.Scope).Plan!;
    var run = Snapshot(DemoAiPreviewCatalog.ProfileId) with { BaselineCatalogId = compatible.Id, ProfileCatalogId = profile.Id, Plan = plan, FrozenInputs = request.Versions };
    Check(DemoAiPreviewProjection.Detail(run) is null, "historical preview stays null " + profile.Id);
    Check(!JsonSerializer.Serialize(request.Versions).Contains("AiPreviewFixtureDigest", StringComparison.Ordinal), "historical saved envelope omits AI field " + profile.Id);
    Check(!DemoAnalysisCatalog.MatchesFrozenFixture(run with { FrozenInputs = request.Versions with { AiPreviewFixtureDigest = new string('a', 64) } }), "historical injected AI lock denied " + profile.Id);
}
try { DemoAiPreviewCatalog.ResponseTemplate("unknown"); Check(false, "unknown response profile accepted"); }
catch (ArgumentException) { Check(true, "no unknown response fallback"); }
Check(!DemoAiPreviewCatalog.MatchesFrozenFixture(null), "missing source refused");
Console.WriteLine($"PASS: {checks} substantive local offline AI catalog/projection assertions.");

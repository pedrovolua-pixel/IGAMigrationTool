using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportDrafts;

var checks = 0;
var input = DraftFixture.Create();
var snapshot = Build(input);
Check("fixed unpublished schema/status", snapshot.SchemaVersion == "synthetic-draft-report-v1" && snapshot.Status == "SyntheticDraft");
Check("independently pinned primitive scores/limitations", snapshot.Content.GetProperty("provisional").GetProperty("display").GetString() == "66.7" &&
    snapshot.Content.GetProperty("publishableCurrent").GetProperty("display").GetString() == "100.0" && snapshot.Content.GetProperty("maturity").GetProperty("level").GetString() == "Initial");
Check("full envelope digest matches SHA256", snapshot.CanonicalContentDigest == Convert.ToHexStringLower(SHA256.HashData(DraftSnapshotBuilder.CanonicalPayload(snapshot))));
Check("repeat bytes/digest", Build(input).CanonicalContentDigest == snapshot.CanonicalContentDigest && DraftSnapshotBuilder.CanonicalPayload(Build(input)).SequenceEqual(DraftSnapshotBuilder.CanonicalPayload(snapshot)));
Check("valid exact snapshot", DraftSnapshotBuilder.ValidateSnapshot(snapshot));
var reordered = Node(input.Content);
Reverse(reordered["healthyControls"]!.AsArray());
var rootReverse = new JsonObject(reordered.Reverse().Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value?.DeepClone())));
var sourceReverse = Node(input.Source.CapabilityLock); Reverse(sourceReverse["modules"]!.AsArray());
Check("declared keyed arrays and properties canonical", Build(input with { Content = Json(rootReverse), Source = input.Source with { CapabilityLock = Json(sourceReverse) } }).CanonicalContentDigest == snapshot.CanonicalContentDigest);
var savedCulture = CultureInfo.CurrentCulture;
try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Check("culture independent", Build(input).CanonicalContentDigest == snapshot.CanonicalContentDigest); }
finally { CultureInfo.CurrentCulture = savedCulture; }
using (var contentDocument = JsonDocument.Parse(input.Content.GetRawText()))
using (var sourceDocument = JsonDocument.Parse(input.Source.AnalysisLock.GetRawText()))
{
    var detached = Build(input with { Content = contentDocument.RootElement, Source = input.Source with { AnalysisLock = sourceDocument.RootElement } });
    contentDocument.Dispose(); sourceDocument.Dispose();
    Check("content and source survive original document disposal", DraftSnapshotBuilder.ValidateSnapshot(detached) && detached.CanonicalContentDigest == snapshot.CanonicalContentDigest);
}
var callerMutable = Node(input.Content);
var frozenBefore = Build(input with { Content = Json(callerMutable) });
callerMutable["findings"]![0]!["title"] = "Changed after return";
Check("caller mutation leaves returned immutable value", frozenBefore.Content.GetProperty("findings")[0].GetProperty("title").GetString() == "Current fictional title" && frozenBefore.CanonicalContentDigest == snapshot.CanonicalContentDigest);
var copiedBytes = DraftSnapshotBuilder.CanonicalPayload(snapshot); copiedBytes[0] = 0;
Check("returned canonical bytes are caller-owned", DraftSnapshotBuilder.ValidateSnapshot(snapshot));
foreach (var source in new[]
{
    input.Source with { RunInputDigest = new string('c',64) }, input.Source with { AnalysisContentDigest = new string('c',64) },
    input.Source with { ScoringContentDigest = new string('c',64) }, input.Source with { SavedCoverageDigest = new string('c',64) },
    input.Source with { ReviewSnapshotDigest = new string('c',64) }, input.Source with { RunRevision = 14, ReviewRunRevision = 14 },
    input.Source with { RunId = Guid.Parse("d6bb0d50-a10d-4bb5-82ce-65cb53918e14"), ReviewRunId = Guid.Parse("d6bb0d50-a10d-4bb5-82ce-65cb53918e14") }
}) Check("each source binding changes digest", Build(input with { Source = source }).CanonicalContentDigest != snapshot.CanonicalContentDigest);
foreach (var (path, value) in new[] { ("title", "<script>alert(1)</script>\n![image](https://invalid.example/x)"), ("facts", "Changed explicit synthetic fact") })
{
    var changed = Node(input.Content);
    if (path == "facts") changed["findings"]![0]!["facts"]![0] = value;
    else { changed["findings"]![0]![path] = value; changed["reviewHistory"]![0]!["events"]![0]!["title"] = value; }
    var result = Build(input with { Content = Json(changed) });
    Check("all data content binds digest and remains plain text", result.CanonicalContentDigest != snapshot.CanonicalContentDigest);
}
var rejected = Node(input.Content); rejected["findings"]![0]!["state"] = "Rejected";
Denied("finding state/history mismatch", input with { Content = Json(rejected) }, DraftReportIssue.SourceMismatch);
Check("forged snapshot digest refused", !DraftSnapshotBuilder.ValidateSnapshot(snapshot with { CanonicalContentDigest = new string('c', 64) }));
Check("wrong snapshot schema refused", !DraftSnapshotBuilder.ValidateSnapshot(snapshot with { SchemaVersion = "published-report-v1" }));
Check("fake published status refused", !DraftSnapshotBuilder.ValidateSnapshot(snapshot with { Status = "Published" }));
Check("noncanonical manually reordered snapshot refused", !DraftSnapshotBuilder.ValidateSnapshot(snapshot with { Content = input.Content }));

var negatives = new (string Name, DraftReportInput? Input, DraftReportIssue Issue)[]
{
    ("null input",null,DraftReportIssue.InvalidInput), ("null source",input with { Source = null! },DraftReportIssue.InvalidInput),
    ("wrong customer",input with { Source = input.Source with { Scope = input.Source.Scope with { CustomerId = "other" } } },DraftReportIssue.WrongScope),
    ("wrong project",input with { Source = input.Source with { Scope = input.Source.Scope with { ProjectId = "other" } } },DraftReportIssue.WrongScope),
    ("wrong environment",input with { Source = input.Source with { Scope = input.Source.Scope with { EnvironmentId = "other" } } },DraftReportIssue.WrongScope),
    ("empty run",input with { Source = input.Source with { RunId = Guid.Empty } },DraftReportIssue.InvalidSource),
    ("wrong run state",input with { Source = input.Source with { RunState = "Completed" } },DraftReportIssue.InvalidSource),
    ("negative revision",input with { Source = input.Source with { RunRevision = -1 } },DraftReportIssue.InvalidSource),
    ("unknown profile",input with { Source = input.Source with { ProfileId = "synthetic-analysis-equal-v1" } },DraftReportIssue.UnknownVersion),
    ("malformed digest",input with { Source = input.Source with { RunInputDigest = "bad" } },DraftReportIssue.InvalidSource),
    ("cross run review",input with { Source = input.Source with { ReviewRunId = Guid.NewGuid() } },DraftReportIssue.SourceMismatch),
    ("mismatched review revision",input with { Source = input.Source with { ReviewRunRevision = 12 } },DraftReportIssue.SourceMismatch),
    ("undefined content",input with { Content = default },DraftReportIssue.InvalidContent),
    ("missing content field",Change(input,"methodology",null,true),DraftReportIssue.InvalidContent),
    ("transport token",Change(input,"csrfToken","not-a-real-token"),DraftReportIssue.InvalidContent),
    ("runtime readtime",Change(input,"observedAtDatabaseUtc","2026-10-02T00:00:00Z"),DraftReportIssue.InvalidContent)
};
foreach (var (name, item, issue) in negatives) Denied(name, item, issue);
var unknownVersion = Node(input.Source.FrozenVersions); unknownVersion["workSchemaVersion"] = "synthetic-work-v2";
Denied("unknown frozen work version", input with { Source = input.Source with { FrozenVersions = Json(unknownVersion) } }, DraftReportIssue.UnknownVersion);
var wrongScope = Node(input.Source.AnalysisLock); wrongScope["scope"]!["projectId"] = "other";
Denied("analysis lock scope mismatch", input with { Source = input.Source with { AnalysisLock = Json(wrongScope) } }, DraftReportIssue.SourceMismatch);
var wrongSource = Node(input.Source.FrozenVersions); wrongSource["analysisFixtureDigest"] = new string('c', 64);
Denied("frozen analysis binding mismatch", input with { Source = input.Source with { FrozenVersions = Json(wrongSource) } }, DraftReportIssue.SourceMismatch);
foreach (var array in new[] { "categories", "findings", "reviewHistory", "healthyControls" })
{
    var duplicate = Node(input.Content); duplicate[array]!.AsArray().Add(duplicate[array]![0]!.DeepClone());
    Denied($"duplicate {array}", input with { Content = Json(duplicate) }, DraftReportIssue.DuplicateId);
}
foreach (var (name, transform, issue) in new (string, Action<JsonObject>, DraftReportIssue)[]
{
    ("invalid health status",node=>node["provisional"]!["status"]="Green",DraftReportIssue.InvalidContent),
    ("empty unavailable disguised perfect",node=>node["publishableCurrent"]!["eligibleUnits"]=0,DraftReportIssue.InvalidContent),
    ("culture-dependent decimal",node=>node["provisional"]!["raw"]="66,6",DraftReportIssue.InvalidContent),
    ("quality count mismatch",node=>node["quality"]!["gapUnits"]=1,DraftReportIssue.InvalidContent),
    ("maturity source digest mismatch",node=>node["maturity"]!["contentDigest"]=new string('c',64),DraftReportIssue.SourceMismatch),
    ("duplicate-domain maturity",node=>node["maturity"]!["domains"]!.AsArray().Add(node["maturity"]!["domains"]![0]!.DeepClone()),DraftReportIssue.DuplicateId),
    ("nonpass healthy control",node=>node["healthyControls"]![0]!["state"]="Finding",DraftReportIssue.InvalidContent),
    ("different review revision",node=>node["reviewHistory"]![0]!["revision"]=2,DraftReportIssue.SourceMismatch),
    ("different original title",node=>node["reviewHistory"]![0]!["originalTitle"]="changed",DraftReportIssue.SourceMismatch),
    ("unknown finding state",node=>node["findings"]![0]!["state"]="ValidatedClosed",DraftReportIssue.InvalidContent),
    ("missing original digest",node=>node["findings"]![0]!["originalDigests"]=new JsonArray(),DraftReportIssue.InvalidContent),
    ("no methodology",node=>node["methodology"]=new JsonArray(),DraftReportIssue.InvalidContent),
    ("no deferred section disclosure",node=>node["unavailableSections"]=new JsonArray(),DraftReportIssue.InvalidContent),
    ("nested authority actions",node=>node["findings"]![0]!["actions"]=new JsonObject(),DraftReportIssue.InvalidContent)
})
{
    var node = Node(input.Content); transform(node); Denied(name, input with { Content = Json(node) }, issue);
}
// Capture a new valid review and prove that an already-returned draft is unchanged.
var newlyReviewed = Node(input.Content);
var nextEvent = newlyReviewed["reviewHistory"]![0]!["events"]![0]!.DeepClone();
nextEvent["eventId"] = "c9d82445-0629-4c90-90f0-8cf1b32c6c39";
nextEvent["revision"] = 2; nextEvent["kind"] = "Reject"; nextEvent["state"] = "Rejected";
nextEvent["reason"] = "Fictional consultant disagreement"; nextEvent["title"] = null; nextEvent["businessContext"] = null;
newlyReviewed["reviewHistory"]![0]!["events"]!.AsArray().Add(nextEvent);
newlyReviewed["reviewHistory"]![0]!["revision"] = 2;
newlyReviewed["findings"]![0]!["revision"] = 2; newlyReviewed["findings"]![0]!["state"] = "Rejected";
var reviewSnapshot = Build(input with { Content = Json(newlyReviewed), Source = input.Source with { ReviewSnapshotDigest = new string('c', 64) } });
Check("new valid review changes digest", reviewSnapshot.CanonicalContentDigest != snapshot.CanonicalContentDigest);
Check("old review draft remains unchanged", snapshot.Content.GetProperty("findings")[0].GetProperty("state").GetString() == "Proposed" && DraftSnapshotBuilder.ValidateSnapshot(snapshot));
var duplicateEvent = newlyReviewed.DeepClone().AsObject();
duplicateEvent["reviewHistory"]![0]!["events"]![1]!["eventId"] = duplicateEvent["reviewHistory"]![0]!["events"]![0]!["eventId"]!.DeepClone();
Denied("duplicate event UUID", input with { Content = Json(duplicateEvent) }, DraftReportIssue.DuplicateId);
var backwardsHistory = newlyReviewed.DeepClone().AsObject(); Reverse(backwardsHistory["reviewHistory"]![0]!["events"]!.AsArray());
Denied("history order is preserved and validated", input with { Content = Json(backwardsHistory) }, DraftReportIssue.InvalidContent);
foreach (var (name, mutate, issue) in new (string, Action<JsonObject>, DraftReportIssue)[]
{
    ("duplicate maturity gate", node => node["maturity"]!["gates"]![1] = node["maturity"]!["gates"]![0]!.DeepClone(), DraftReportIssue.DuplicateId),
    ("duplicate indicator kind", node => node["maturity"]!["domains"]![0]!["indicators"]![1] = node["maturity"]!["domains"]![0]!["indicators"]![0]!.DeepClone(), DraftReportIssue.DuplicateId),
    ("unknown indicator state", node => node["maturity"]!["domains"]![0]!["indicators"]![0]!["state"] = "Approved", DraftReportIssue.InvalidContent),
    ("insufficient indicator missing reason", node => node["maturity"]!["domains"]![0]!["indicators"]![0]!["reasonCode"] = null, DraftReportIssue.InvalidContent),
    ("missing ownership", node => node["maturity"]!["ownership"] = null, DraftReportIssue.InvalidContent),
    ("missing indicator field", node => node["maturity"]!["domains"]![0]!["indicators"]![0]!.AsObject().Remove("assessmentReferences"), DraftReportIssue.InvalidContent),
    ("history title disagrees", node => node["reviewHistory"]![0]!["events"]![0]!["title"] = "Different", DraftReportIssue.SourceMismatch),
    ("history actor missing", node => node["reviewHistory"]![0]!["events"]![0]!["actorRoles"] = new JsonArray(), DraftReportIssue.InvalidContent),
    ("empty event UUID", node => node["reviewHistory"]![0]!["events"]![0]!["eventId"] = Guid.Empty.ToString(), DraftReportIssue.InvalidContent)
}) { var node = Node(input.Content); mutate(node); Denied(name, input with { Content = Json(node) }, issue); }
foreach (var field in new[] { "categories", "objectTypes", "modules", "quality", "reviewHistory", "healthyControls" })
{
    var node = Node(input.Content);
    var row = field == "quality" ? node[field]! : node[field]![0]!;
    row["unknownField"] = "unapproved extra content";
    Denied("unknown nested " + field, input with { Content = Json(node) }, DraftReportIssue.InvalidContent);
}
var random = new Random(1729);
for (var iteration = 0; iteration < 100; iteration++)
{
    var node = Node(input.Content);
    var shuffled = new JsonObject(node.OrderBy(_ => random.Next()).Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value?.DeepClone())));
    if (random.Next(2) == 0) Reverse(shuffled["healthyControls"]!.AsArray());
    Check("random property/set ordering preserves identity", Build(input with { Content = Json(shuffled) }).CanonicalContentDigest == snapshot.CanonicalContentDigest);
}
Console.WriteLine($"{checks} pure draft-report checks passed; no routes, storage, publication or authority.");

static JsonObject Node(JsonElement element) => JsonNode.Parse(element.GetRawText())!.AsObject();
static JsonElement Json(JsonNode node) => DraftFixture.Json(node);
static void Reverse(JsonArray array) { var nodes = array.Reverse().Select(node => node!.DeepClone()).ToArray(); array.Clear(); foreach (var node in nodes) array.Add(node); }
static DraftReportInput Change(DraftReportInput input, string property, object? value, bool remove = false)
{ var node = Node(input.Content); if (remove) node.Remove(property); else node[property] = JsonSerializer.SerializeToNode(value); return input with { Content = Json(node) }; }
static DraftReportSnapshot Build(DraftReportInput input) => DraftSnapshotBuilder.Build(input).Snapshot ?? throw new Exception($"Unexpected draft denial {DraftSnapshotBuilder.Build(input).Issue}");
void Denied(string name, DraftReportInput? input, DraftReportIssue issue)
{ var result = DraftSnapshotBuilder.Build(input); Check(name, result.Issue == issue && result.Snapshot is null && !result.Succeeded); }
void Check(string name, bool condition) { if (!condition) throw new Exception(name); checks++; }

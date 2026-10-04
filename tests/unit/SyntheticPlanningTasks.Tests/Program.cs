using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RecommendationGuidance;
using SyntheticFixPackages;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal static class Program
{
    internal static int Checks;
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    internal static string Fixtures = "";
    internal static void Check(bool value, string name) { Checks++; if (!value) throw new InvalidOperationException("FAIL: " + name); }
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    // Independent normative serialization, not the production canonical helper.
    internal static string Canonical<T>(T value)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) Write(writer, JsonSerializer.SerializeToElement(value, Json));
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var item in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal)) { writer.WritePropertyName(item.Name); Write(writer, item.Value); } writer.WriteEndObject(); }
        else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    internal static T Literal<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Fixtures, name + ".json")), Json)!;
    internal static GuidanceInput Input()
    {
        var input = GuidanceFixture.Create(); var versions = JsonNode.Parse(input.Source.FrozenVersions.GetRawText())!.AsObject();
        versions["applicationVersion"] = "synthetic-planning-tasks-app-v1";
        versions["fixPackageTemplateDigest"] = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
        versions["fixReviewContractDigest"] = "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f";
        versions["planningTaskContractDigest"] = "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2";
        return input with { Source = input.Source with { ProfileId = "synthetic-review-maturity-planning-tasks-equal-v1", FrozenVersions = JsonSerializer.SerializeToElement(versions) } };
    }
    internal static FixPackageSnapshot Package(GuidanceInput input)
    {
        var guidance = RecommendationGuidanceBuilder.Build(input); Check(guidance.Succeeded, "real guidance builder");
        var result = FixPackageBuilder.Build(guidance.Snapshot); Check(result.Succeeded, "real package builder"); return result.Snapshot!;
    }
    internal static PlanningTaskAuthority Authority => new("synthetic-consultant", true, true, false, true, PlanningTaskScope.Fixed,
        [PlanningTaskRole.Consultant], ["SECURITY", "OPERATIONS"], [PlanningTaskAction.Read, PlanningTaskAction.Create, PlanningTaskAction.Manage, PlanningTaskAction.Comment], PlanningTaskResourceState.Mutable);
    internal static ArtifactReviewSnapshot Overlay(FixPackageSnapshot package, int reviewed = 0)
    {
        var source = ArtifactReviewSourceBuilder.Build(package); Check(source.Succeeded, "actual artifact source");
        var sorted = source.Source!.Artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.ArtifactId).ToArray();
        var entries = source.Source.Artifacts.Select(artifact =>
        {
            var index = Array.IndexOf(sorted, artifact.ArtifactId);
            var history = index < reviewed ? ImmutableArray.Create(new ArtifactReviewEvent(Guid.Parse("11111111-1111-4111-8111-" + (index + 1).ToString("D12")), 1, ArtifactReviewKind.ReviewForPlanning,
                Authority.ActorId, ["Consultant"], DateTimeOffset.Parse("2026-10-03T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "Fictional review", source.Source.Binding, ArtifactReviewState.ReviewedForPlanning)) : [];
            return new ArtifactReviewEntry(artifact, history.Length, history.IsEmpty ? ArtifactReviewState.Unverified : ArtifactReviewState.ReviewedForPlanning, history.IsEmpty, !history.IsEmpty, history);
        }).ToImmutableArray();
        return new(source.Source.Binding, Authority.ActorId, entries);
    }
    internal static PlanningTaskSource Source(GuidanceInput input, int reviewed = 3)
    { var package = Package(input); var result = PlanningTaskSourceBuilder.Build(package, Overlay(package, reviewed)); Check(result.Succeeded, "actual complete task source"); return result.Source!; }
    private static async Task Main(string[] args)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "independent-oracle.py"))) directory = directory.Parent;
        Fixtures = directory?.FullName ?? throw new InvalidOperationException("Independent fixture packet missing.");
        Check(PlanningTaskSourceBuilder.ContractDigest == "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2", "literal frozen contract");
        foreach (var name in new[] { "normal", "empty", "multi", "hostile" })
        {
            var input = Input();
            if (name == "empty") input = input with { Findings = [] };
            if (name == "multi")
            {
                var first = input.Findings[0] with { Options = [.. input.Findings[0].Options, new("compare-fixture", "Compare fictional evidence.", "A separate fixture run.", "No closure claim.", "Retain originals.")], Occurrences = [.. input.Findings[0].Occurrences, new(new string('f', 64), "OBJECT-2", "SyntheticControl", "SyntheticOperations", GuidanceFixture.Digest, "fixture-evidence:OBJECT-2")] };
                var second = input.Findings[0] with { FindingId = new string('d', 64), Occurrences = [new(new string('e', 64), "OBJECT-3", "SyntheticControl", "SyntheticSecurity", GuidanceFixture.Digest, "fixture-evidence:OBJECT-3")] };
                input = input with { Findings = [second, first] };
            }
            if (name == "hostile")
            {
                const string hostile = "<script>alert('x')</script> \0\r\né e\u0301 😀 \" + & ` =SUM(A1)\nhttps://invalid.example/evil";
                input = input with { Findings = [input.Findings[0] with { OriginalTitle = hostile, PresentationTitle = hostile, BusinessContext = hostile, Options = [input.Findings[0].Options[0] with { Text = hostile }] }] };
            }
            var package = Package(input); var overlay = Overlay(package); var result = PlanningTaskSourceBuilder.Build(package, overlay);
            Check(result.Succeeded, name + " source proof accepted");
            Check(package.CanonicalJson == File.ReadAllText(Path.Combine(Fixtures, name + "-golden.json")), name + " independent whole canonical package");
            Check(Canonical(result.Source!.Binding) == File.ReadAllText(Path.Combine(Fixtures, name + "-task-binding.json")), name + " every literal binding field");
            Check(Canonical(result.Source.Options) == File.ReadAllText(Path.Combine(Fixtures, name + "-task-options.json")), name + " whole typed identities/vectors/order");
            Check(result.Source.Options.All(item => !item.CanCreate), name + " initial unreviewed never healthy/convertible");
            foreach (var property in new[] { "ContentDigest", "CanonicalJson", "TemplateDigest", "Disclaimer", "SchemaVersion", "Status", "TemplateVersion" })
            {
                var forged = Package(input); typeof(FixPackageSnapshot).GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(forged, "forged");
                Check(!PlanningTaskSourceBuilder.Build(forged, overlay).Succeeded, name + " forged internals denied " + property);
            }
        }
        var normal = Package(Input()); var ready = Overlay(normal, 3); var sourceReady = PlanningTaskSourceBuilder.Build(normal, ready);
        Check(sourceReady.Succeeded && sourceReady.Source!.Options.Single().CanCreate, "three latest current reviews eligible");
        Check(Canonical(sourceReady.Source!.Options.Single().CurrentAttestations) == File.ReadAllText(Path.Combine(Fixtures, "normal-reviewed-vector.json")), "independent complete reviewed vector");
        var command = Literal<PlanningTaskCommand>("normal-command");
        Check(PlanningTaskPolicy.ValidateCommand(command) is null, "exact Unicode control reason command");
        foreach (var role in Enum.GetValues<PlanningTaskRole>()) Check(PlanningTaskPolicy.Authorize(Authority with { Roles = [role] }, PlanningTaskScope.Fixed) == (role == PlanningTaskRole.Consultant ? null : PlanningTaskIssue.Denied), "sole role " + role);
        foreach (var role in Enum.GetValues<PlanningTaskRole>()) Check(PlanningTaskPolicy.Authorize(Authority with { Roles = [PlanningTaskRole.Consultant, role] }, PlanningTaskScope.Fixed) == PlanningTaskIssue.Denied, "mixed role " + role);
        foreach (var action in Enum.GetValues<PlanningTaskAction>()) Check(PlanningTaskPolicy.Authorize(Authority with { Actions = [PlanningTaskAction.Read] }, PlanningTaskScope.Fixed, action) == (action == PlanningTaskAction.Read ? null : PlanningTaskIssue.Denied), "read-only action " + action);
        foreach (var denied in new[] { Authority with { Authenticated = false }, Authority with { Active = false }, Authority with { Revoked = true }, Authority with { AssignmentActive = false }, Authority with { AssignedScope = PlanningTaskScope.Fixed with { ProjectId = "foreign" } }, Authority with { Actions = default }, Authority with { Categories = [] }, Authority with { ActorId = "" } }) Check(PlanningTaskPolicy.Authorize(denied, PlanningTaskScope.Fixed) == PlanningTaskIssue.Denied, "trusted identity denial");
        foreach (var state in Enum.GetValues<PlanningTaskResourceState>()) Check(PlanningTaskPolicy.Authorize(Authority with { ResourceState = state }, PlanningTaskScope.Fixed) == (state == PlanningTaskResourceState.Mutable ? null : PlanningTaskIssue.Denied), "resource state " + state);
        Check(PlanningTaskPolicy.Authorize(Authority with { Categories = ["OPERATIONS"] }, PlanningTaskScope.Fixed, category: "SECURITY") == PlanningTaskIssue.Denied, "category authority");
        foreach (var invalid in new[] { command with { EventId = Guid.Empty }, command with { Kind = (PlanningTaskKind)999 }, command with { ExpectedRevision = -1 }, command with { ExpectedRevision = 1 }, command with { ExpectedRevision = 9007199254740992 }, command with { ExpectedSourceDigest = "wrong" }, command with { ExpectedAttestations = [] }, command with { ExpectedAttestations = default }, command with { ExpectedAttestations = command.ExpectedAttestations.Reverse().ToImmutableArray() }, command with { Reason = " \r\n\t" }, command with { Reason = new string('x', 2001) }, command with { Reason = "\uD800" }, command with { Reason = "\uDC00" } }) Check(PlanningTaskPolicy.ValidateCommand(invalid) == PlanningTaskIssue.InvalidInput, "closed bounded command denied");
        Check(PlanningTaskPolicy.ValidateCommand(command with { Reason = new string('x', 2000) }) is null, "exact 2000 UTF16 boundary");
        foreach (var item in new[] { command.ExpectedAttestations[0] with { Revision = 0 }, command.ExpectedAttestations[0] with { EventId = null }, command.ExpectedAttestations[0] with { Kind = null }, command.ExpectedAttestations[0] with { State = (ArtifactReviewState)999 }, command.ExpectedAttestations[0] with { Revision = 9007199254740992 }, command.ExpectedAttestations[0] with { SourceDigest = null } }) Check(!PlanningTaskPolicy.ValidVector(command.ExpectedAttestations.SetItem(0, item)), "invalid vector field denied");
        foreach (var status in Enum.GetValues<PlanningTaskStatus>()) foreach (var freshness in new[] { PlanningTaskFreshness.CurrentPlan, PlanningTaskFreshness.NeedsReconfirmation }) foreach (var finding in new[] { "Confirmed", "Proposed", "Deferred", "Rejected" }) foreach (var reviewed in new[] { true, false }) foreach (var kind in Enum.GetValues<PlanningTaskKind>())
        {
            var always = new Dictionary<PlanningTaskStatus, PlanningTaskKind[]> { [PlanningTaskStatus.Planned] = [PlanningTaskKind.Cancel, PlanningTaskKind.Comment], [PlanningTaskStatus.InProgress] = [PlanningTaskKind.ReturnToPlanned, PlanningTaskKind.Cancel, PlanningTaskKind.Comment], [PlanningTaskStatus.Completed] = [PlanningTaskKind.Comment], [PlanningTaskStatus.Cancelled] = [PlanningTaskKind.Comment] };
            var allowed = always[status].Contains(kind) || finding != "Rejected" && (kind == PlanningTaskKind.Reopen && status is PlanningTaskStatus.Completed or PlanningTaskStatus.Cancelled || freshness == PlanningTaskFreshness.CurrentPlan && (kind == PlanningTaskKind.StartProgress && status == PlanningTaskStatus.Planned || kind == PlanningTaskKind.Complete && status == PlanningTaskStatus.InProgress) || kind == PlanningTaskKind.ReconfirmPlan && freshness == PlanningTaskFreshness.NeedsReconfirmation && reviewed && status is PlanningTaskStatus.Planned or PlanningTaskStatus.InProgress);
            Check(PlanningTaskPolicy.Transition(status, freshness, finding, reviewed, kind) == (allowed ? null : PlanningTaskIssue.InvalidState), "normative lifecycle cross product");
        }
        var firstEntry = ready.Entries[0];
        foreach (var bad in new[] { ready with { Source = ready.Source with { SourceDigest = new string('0', 64) } }, ready with { Entries = [] }, ready with { Entries = ready.Entries.SetItem(0, firstEntry with { Revision = 2 }) }, ready with { Entries = ready.Entries.SetItem(0, firstEntry with { CanReview = true }) }, ready with { Entries = ready.Entries.SetItem(0, firstEntry with { History = [firstEntry.History[0] with { Revision = 2 }] }) }, ready with { Entries = ready.Entries.SetItem(0, firstEntry with { History = [firstEntry.History[0] with { Reason = "\uD800" }] }) }, ready with { Entries = ready.Entries.SetItem(0, firstEntry with { Artifact = firstEntry.Artifact with { Kind = "Other" } }) } }) Check(!PlanningTaskSourceBuilder.Build(normal, bad).Succeeded, "forged typed overlay denied");
        foreach (var property in new[] { "GuidanceDigest", "FindingReviewDigest" })
        {
            var old = firstEntry.History[0];
            var changed = property == "GuidanceDigest" ? old.Source with { GuidanceDigest = new string('0', 64) } : old.Source with { FindingReviewDigest = new string('0', 64) };
            var forged = ready with { Entries = ready.Entries.SetItem(0, firstEntry with { History = [old with { Source = changed }] }) };
            Check(!PlanningTaskSourceBuilder.Build(normal, forged).Succeeded, "same package historical full binding denied " + property);
        }
        var reusedArtifactEvent = ready with { Entries = ready.Entries.Select(entry => entry with { History = [entry.History[0] with { EventId = ready.Entries[0].History[0].EventId }] }).ToImmutableArray() };
        Check(PlanningTaskSourceBuilder.Build(normal, reusedArtifactEvent).Succeeded, "legacy per-artifact UUID namespace preserved");
        foreach (var status in Enum.GetValues<PlanningTaskStatus>()) foreach (var kind in Enum.GetValues<PlanningTaskKind>())
            Check(PlanningTaskPolicy.Transition(status, PlanningTaskFreshness.SourceUnavailable, "Confirmed", true, kind) == PlanningTaskIssue.InvalidState, "unavailable transition never grants write");
        Check(PlanningTaskPolicy.Transition((PlanningTaskStatus)999, PlanningTaskFreshness.CurrentPlan, "Confirmed", true, PlanningTaskKind.Comment) == PlanningTaskIssue.InvalidState, "undefined status closed");
        Check(PlanningTaskPolicy.Transition(PlanningTaskStatus.Planned, (PlanningTaskFreshness)999, "Confirmed", true, PlanningTaskKind.Comment) == PlanningTaskIssue.InvalidState, "undefined freshness closed");
        Check(PlanningTaskSourceBuilder.Build(null, ready).Issue == PlanningTaskIssue.SourceUnavailable && PlanningTaskSourceBuilder.Build(normal, null).Issue == PlanningTaskIssue.SourceUnavailable, "missing proof denied");
        var count = 0; var guarded = new SyntheticPlanningTaskStore("Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle14_unopened;Username=iga_synthetic", PlanningTaskScope.Fixed,
            (_, _, _, _, _) => { count++; return Task.FromResult(new PlanningTaskSourceResult(PlanningTaskIssue.SourceUnavailable, null)); });
        Check((await guarded.ReadAsync(Guid.Empty, Authority)).Issue == PlanningTaskIssue.InvalidInput, "invalid read before connection");
        Check((await guarded.ApplyAsync(Guid.NewGuid(), command.ExpectedAttestations[0].ArtifactId, Authority with { Authenticated = false }, command)).Issue == PlanningTaskIssue.Denied, "denied command before connection");
        Check(count == 0, "denied requests never callback");
        foreach (var database in new[] { "Host=remote.example;Database=iga_synthetic_cycle14_x", "Host=127.0.0.1;Database=iga_synthetic_cycle13_x", "Host=127.0.0.1;Database=customer" })
        { try { _ = new SyntheticPlanningTaskStore(database, PlanningTaskScope.Fixed, (_, _, _, _, _) => Task.FromResult(new PlanningTaskSourceResult(null, null))); Check(false, "unsafe database constructor"); } catch (ArgumentException) { Check(true, "guarded dedicated database"); } }
        if (args.Contains("--database", StringComparer.Ordinal)) await DatabaseCases.Run();
        Console.WriteLine($"PASS {Checks} actual assertions ({(args.Contains("--database", StringComparer.Ordinal) ? "portable + owned PostgreSQL" : "portable; PostgreSQL NOT EXECUTED")})");
    }
}

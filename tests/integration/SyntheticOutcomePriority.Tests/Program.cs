using System.Collections.Immutable;
using System.Globalization;
using System.Diagnostics;
using Npgsql;
using AssessmentCoverage;
using AssessmentScoring;
using SyntheticOutcomePriority;
using static Fixtures;

const string database = "iga_synthetic_phase1b_op_tests";
const string connectionString = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_phase1b_op_tests;Username=iga_synthetic";
if (args.Contains("--crash-uncommitted", StringComparer.Ordinal))
{
    var childStore = new SyntheticOutcomePriorityStore(OutcomeScope.Fixed, async (c, t, run, _, ct) =>
    {
        await using var q = new NpgsqlCommand("SELECT source_json FROM public.op_test_sources WHERE run_id=@run", c, t); q.Parameters.AddWithValue("run", run);
        return new(null, OutcomePriorityCanonical.Parse<Phase1BPlanningSource>((string)(await q.ExecuteScalarAsync(ct))!));
    });
    await using var childConnection = await Open(); await using var childTransaction = await childConnection.BeginTransactionAsync();
    var childRead = await childStore.ReadPlanningAsync(childConnection, childTransaction, Run, Consultant);
    var childEntry = childRead.Snapshot!.Entries.Single();
    var childCommand = new PlanningCommand(Guid.NewGuid(), PlanningKind.OverridePriority, childEntry.Original.OptionId, childEntry.Revision, childRead.Snapshot.Source.SourceDigest, PriorityBand.Immediate, null, [], "Uncommitted crash fixture.");
    var childWrite = await childStore.ApplyPlanningAsync(childConnection, childTransaction, Run, Consultant, childCommand);
    if (childWrite.Issue is not null) throw new Exception("childwrite");
    Console.WriteLine("READY_UNCOMMITTED"); await Task.Delay(Timeout.Infinite); return;
}
await using (var admin = new NpgsqlConnection("Host=127.0.0.1;Port=55433;Database=postgres;Username=iga_synthetic"))
{
    await admin.OpenAsync();
    await using var check = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin); check.Parameters.AddWithValue("name", database);
    if (Convert.ToInt32(await check.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0) { await using var create = new NpgsqlCommand("CREATE DATABASE iga_synthetic_phase1b_op_tests", admin); await create.ExecuteNonQueryAsync(); }
}
await Db("DROP SCHEMA IF EXISTS synthetic_outcome_priority CASCADE; DROP TABLE IF EXISTS public.op_test_sources; CREATE TABLE public.op_test_sources(run_id uuid PRIMARY KEY, source_json text NOT NULL)");
var store = new SyntheticOutcomePriorityStore(OutcomeScope.Fixed, async (c, t, run, _, ct) =>
{
    await using var q = new NpgsqlCommand("SELECT source_json FROM public.op_test_sources WHERE run_id=@run", c, t); q.Parameters.AddWithValue("run", run);
    var raw = await q.ExecuteScalarAsync(ct);
    return raw is null ? new(OutcomePriorityIssue.SourceUnavailable, null) : new(null, OutcomePriorityCanonical.Parse<Phase1BPlanningSource>((string)raw));
});
await using (var c = await Open()) { await store.InitializeAsync(c); await store.InitializeAsync(c); }
await Db("INSERT INTO public.op_test_sources VALUES(@run,@json)", ("run", Run), ("json", OutcomePriorityCanonical.Json(Source())));

async Task<OutcomeRegistrySnapshot> Registry(OutcomeAuthority? a = null) => await Tx(async (c, t) =>
{ var r = await store.ReadRegistryAsync(c, t, a ?? Consultant); Check(r.Issue is null && r.Snapshot is not null, "verifiedregistry"); return r.Snapshot!; });
async Task<OutcomeCommand> Command(OutcomeKind kind, string id, long version, Guid? eventId = null)
{
    var registry = await Registry(); var entry = registry.Entries.SingleOrDefault(e => e.Content.OutcomeId == id && e.Content.Version == version);
    var content = kind == OutcomeKind.CreateDraft ? Content(id, version) : null;
    return new(eventId ?? Guid.NewGuid(), kind, id, version, entry?.Revision ?? 0, content?.ContentDigest ?? entry!.Content.ContentDigest, registry.Revision, kind == OutcomeKind.Approve ? entry!.History[^1].EventId : null, content, "Explicit fictional decision.");
}
async Task<OutcomeApplyResult> Apply(OutcomeCommand command, OutcomeAuthority? a = null, SyntheticOutcomePriorityStore? target = null) => await Tx((c, t) => (target ?? store).ApplyOutcomeAsync(c, t, a ?? Consultant, command));
async Task<PlanningReadResult> Planning() => await Tx((c, t) => store.ReadPlanningAsync(c, t, Run, Consultant));
async Task<PlanningCommand> PlanningCommand(PlanningKind kind, PriorityBand? priority = null, EffortSize? size = null)
{
    var read = await Planning(); Check(read.Issue is null, "planningread"); var entry = read.Snapshot!.Entries.Single();
    return new(Guid.NewGuid(), kind, entry.Original.OptionId, entry.Revision, read.Snapshot.Source.SourceDigest, priority, size, size is null ? [] : ["Explicit fictional sizing assumption."], "Reason retained exactly.");
}
async Task<PlanningApplyResult> Plan(PlanningCommand command, OutcomeAuthority? a = null, SyntheticOutcomePriorityStore? target = null) => await Tx((c, t) => (target ?? store).ApplyPlanningAsync(c, t, Run, a ?? Consultant, command));

var draft1 = await Command(OutcomeKind.CreateDraft, "access-governance", 1); Check((await Apply(draft1)).Issue is null, "draft1");
var rowsBefore = await Rows(); Check((await Apply(draft1)).AlreadyApplied, "exactdraftreplay"); Check(await Rows() == rowsBefore, "replaynowrites");
Check((await Apply(draft1 with { Reason = "changed" })).Issue == OutcomePriorityIssue.EventConflict, "changeduuid");
var directApprove = await Command(OutcomeKind.Approve, "access-governance", 1); Check((await Apply(directApprove, Approver)).Issue == OutcomePriorityIssue.InvalidState, "nodirectdraftapproval");
var review1 = await Command(OutcomeKind.Review, "access-governance", 1); Check((await Apply(review1)).Issue is null, "review1");
var approval1 = await Command(OutcomeKind.Approve, "access-governance", 1);
Check((await Apply(approval1)).Issue == OutcomePriorityIssue.Denied, "consultantcannotapprove");
Check((await Apply(approval1, Approver)).Issue is null, "distinctapprover1");
var approved1 = (await Registry()).Entries.Single(); Check(approved1.State == OutcomeState.CustomerApproved && approved1.Content.Origin == OutcomeOrigin.Inferred, "inferencepreserved");
var selected1 = ImmutableArray.Create(new OutcomeSelection(approved1.Content.OutcomeId, 1, approved1.Content.ContentDigest, approved1.Revision, approved1.History[^1].EventId));
var locked1 = await Tx((c, t) => store.LockOutcomesAsync(c, t, Run, Consultant, selected1)); Check(locked1.Issue is null && SyntheticOutcomePriorityStore.ValidLock(locked1.Lock!), "exactlock1");
Check(SyntheticOutcomePriorityStore.ToScoringOutcomes(locked1.Lock!)!.Value.Single().IsCustomerApprovedFixtureFlag, "verifiedadapterflag");
Check(SyntheticOutcomePriorityStore.ValidateApplicability(locked1.Lock!, [new("object-det", "CONFIGURATION"), new("object-ai", "CONFIGURATION")]) is null, "exactapplicability");
Check(SyntheticOutcomePriorityStore.ValidateApplicability(locked1.Lock!, [new("foreign", "CONFIGURATION")]) == OutcomePriorityIssue.IntegrityMismatch, "foreignapplicability");
Check((await Tx((c, t) => store.LockOutcomesAsync(c, t, Guid.NewGuid(), Consultant, selected1.SetItem(0, selected1[0] with { ContentDigest = new('d', 64) })))).Issue == OutcomePriorityIssue.SourceConflict, "wronglockproof");
var keys = new CoverageKey[] { new("object-det", "CONFIGURATION"), new("object-ai", "CONFIGURATION") };
var coverage = new CoverageItem[] { new(keys[0], CoverageState.Pass, "pass", "deterministic", "fixture"), new(keys[1], CoverageState.Finding, "finding", "ai", "fixture") };
var units = new ScoringUnit[] { new(keys[0], "SECURITY", "object", "module", [], CoverageState.Pass, 1, null, ScoringDetectionMethod.Deterministic, null, 100), new(keys[1], "OPERATIONS", "object", "module", [], CoverageState.Finding, 1, ScoringSeverity.High, ScoringDetectionMethod.AI, ScoringFindingState.Proposed, 80) };
var mapped = SyntheticOutcomePriorityStore.ApplyOutcomeLinks(locked1.Lock!, units, keys);
Check(mapped is not null && mapped.Value.All(u => u.OutcomeIds.SequenceEqual(new[] { "access-governance" })), "verifiedlinkadapter");
var scoring = PilotHealthScorer.Project(new(new(PilotHealthScorer.AlgorithmVersion, PilotHealthScorer.InputSchemaVersion, "fixture-baseline", "fixture-catalog", "fixture-profile", Hash, Hash), new("fixture-profile", ScoringWeightMode.EqualAssessedCategories, [new("SECURITY", 1), new("OPERATIONS", 1)]), keys, coverage, mapped!.Value, SyntheticOutcomePriorityStore.ToScoringOutcomes(locked1.Lock!)!.Value));
Check(scoring.HasProjection && scoring.Projection!.Provisional.Overall.RawScore == 68 && scoring.Projection.PublishableCurrent.Overall.RawScore == 100 && scoring.Projection.Provisional.ApprovedOutcomes.Single().Score.RawScore == 68, "literal68outcomehealth");
Check(SyntheticOutcomePriorityStore.ApplyOutcomeLinks(locked1.Lock!, units.Take(1).ToArray(), keys) is null, "missingunitadapterdeny");


// Keep reviewed v2 pending, then approve v3 and retire it; v2 must never backfill that authority.
Check((await Apply(await Command(OutcomeKind.CreateDraft, "access-governance", 2))).Issue is null, "draft2");
Check((await Apply(await Command(OutcomeKind.Review, "access-governance", 2))).Issue is null, "review2");
Check((await Apply(await Command(OutcomeKind.CreateDraft, "access-governance", 3))).Issue is null, "draft3");
Check((await Apply(await Command(OutcomeKind.Review, "access-governance", 3))).Issue is null, "review3");
var approval3 = await Command(OutcomeKind.Approve, "access-governance", 3); Check((await Apply(approval3, Approver)).Issue is null, "approval3atomic");
var after3 = await Registry(); Check(after3.Entries.Single(e => e.Content.Version == 1).State == OutcomeState.Superseded && after3.Entries.Single(e => e.Content.Version == 3).State == OutcomeState.CustomerApproved, "supersessionpair");
var retired3 = await Command(OutcomeKind.Retire, "access-governance", 3); Check((await Apply(retired3)).Issue == OutcomePriorityIssue.InvalidState, "consultantcannotretireapproved");
Check((await Apply(retired3, Approver)).Issue is null, "customerretirement");
Check((await Apply(retired3, Approver)).AlreadyApplied, "customerretirementhistoricalreplay");
Check((await Apply(await Command(OutcomeKind.Approve, "access-governance", 2), Approver)).Issue == OutcomePriorityIssue.InvalidState, "highwaterretirement");
Check((await Registry()).HighWater.Single().HighestApprovedVersion == 3, "highwater3");
var historic = await Tx((c, t) => store.ReadLockedAsync(c, t, Run, Consultant)); Check(historic.Issue is null && OutcomePriorityCanonical.Json(historic.Lock) == OutcomePriorityCanonical.Json(locked1.Lock), "lockedhistoricalunchanged");
Check((await Tx((c, t) => store.LockOutcomesAsync(c, t, Guid.NewGuid(), Consultant, selected1))).Issue is not null, "cannotnewlocksuperseded");
Check((await Tx((c, t) => store.LockOutcomesAsync(c, t, Guid.NewGuid(), Consultant, []))).Lock is { Outcomes.Length: 0 }, "emptylockvalid");
Check((await Apply(approval1, Approver)).AlreadyApplied, "originalapprovalreplayafterretire");
Check((await Apply(review1)).AlreadyApplied, "historicalreviewreplay");

// Fail before any owned write; caller rollback preserves every row.
var fault = new SyntheticOutcomePriorityStore(OutcomeScope.Fixed, null, new ThrowObserver());
var draft4 = await Command(OutcomeKind.CreateDraft, "access-governance", 4); rowsBefore = await Rows();
Check((await Apply(draft4, target: fault)).Issue == OutcomePriorityIssue.IntegrityMismatch, "faulttyped"); Check(await Rows() == rowsBefore, "faultrollback");
Check((await Apply(draft4)).Issue is null, "draft4"); Check((await Apply(await Command(OutcomeKind.Review, "access-governance", 4))).Issue is null, "review4");
var approval4 = await Command(OutcomeKind.Approve, "access-governance", 4); Check((await Apply(approval4, Approver)).Issue is null, "highwateradvance4");
var v4 = (await Registry()).Entries.Single(e => e.Content.Version == 4);
var lock4 = await Tx((c, t) => store.LockOutcomesAsync(c, t, Guid.NewGuid(), Consultant, [new(v4.Content.OutcomeId, 4, v4.Content.ContentDigest, v4.Revision, v4.History[^1].EventId)]));
Check(lock4.Lock!.ContentDigest != locked1.Lock!.ContentDigest, "newrunnewproof");

var initialPlanning = await Planning(); Check(initialPlanning.Snapshot!.Entries.Single().EffortApproval == EffortApprovalState.Proposed && initialPlanning.Snapshot.Entries.Single().OriginalPriority.RawPriority == 51.75m, "planningoriginal");
var replacement = await PlanningCommand(PlanningKind.ReplaceEffort, size: EffortSize.L); Check((await Plan(replacement)).Issue is null, "replaceeffortapproved");
var afterReplacement = (await Planning()).Snapshot!.Entries.Single(); Check(afterReplacement.EffectiveEffort!.MinimumPersonHours == 24 && afterReplacement.EffortApproval == EffortApprovalState.ApprovedReplacement && afterReplacement.OriginalPriority.RawPriority == 51.75m, "originalpreservednorecalc");
var withdrawal = await PlanningCommand(PlanningKind.WithdrawEffortOverride); Check((await Plan(withdrawal)).Issue is null, "withdrawreplacement");
Check((await Planning()).Snapshot!.Entries.Single().EffortApproval == EffortApprovalState.Proposed, "neverapprovedoriginalrestoresproposed");
Check((await Plan(await PlanningCommand(PlanningKind.WithdrawEffortOverride))).Issue == OutcomePriorityIssue.InvalidState, "redundantwithdrawdeny");
Check((await Plan(await PlanningCommand(PlanningKind.ApproveOriginalEffort))).Issue is null, "approveoriginal");
Check((await Plan(await PlanningCommand(PlanningKind.ReplaceEffort, size: EffortSize.XS))).Issue is null, "replacementXS");
Check((await Plan(await PlanningCommand(PlanningKind.WithdrawEffortOverride))).Issue is null, "withdrawsecondreplacement");
Check((await Planning()).Snapshot!.Entries.Single().EffortApproval == EffortApprovalState.ApprovedOriginal, "approvedoriginalrestoresapproved");
var priority = await PlanningCommand(PlanningKind.OverridePriority, PriorityBand.Immediate); Check((await Plan(priority)).Issue is null, "overridepriority");
Check((await Planning()).Snapshot!.Entries.Single().EffectivePriority == PriorityBand.Immediate, "effectiveoverride");
var unprioritize = await PlanningCommand(PlanningKind.WithdrawPriorityOverride); Check((await Plan(unprioritize)).Issue is null, "withdrawpriority");
Check((await Planning()).Snapshot!.Entries.Single().EffectivePriority == PriorityBand.Medium, "restoreoriginalpriority");
Check((await Plan(priority)).AlreadyApplied, "historicalpriorityreplay");
Check((await Plan(priority with { PriorityOverride = PriorityBand.Low })).Issue == OutcomePriorityIssue.EventConflict, "prioritychangedreplay");

// A changed source retains history and requires fresh explicit decisions; old exact commands may replay.
var stale = await PlanningCommand(PlanningKind.OverridePriority, PriorityBand.High);
await Db("UPDATE public.op_test_sources SET source_json=@json WHERE run_id=@run", ("run", Run), ("json", OutcomePriorityCanonical.Json(Source(2))));
Check((await Plan(stale)).Issue == OutcomePriorityIssue.SourceConflict, "stalenewsource");
var fresh = (await Planning()).Snapshot!.Entries.Single(); Check(fresh.EffortApproval == EffortApprovalState.Proposed && !fresh.HasEffortOverride && !fresh.HasPriorityOverride && fresh.History.Length == 7, "newsourceapprovalsnotrebound");
Check((await Plan(priority)).AlreadyApplied, "oldacceptedreplaycurrentverifiedsource");
Check((await Plan(await PlanningCommand(PlanningKind.WithdrawPriorityOverride))).Issue == OutcomePriorityIssue.InvalidState, "newsourcecannotwithdrawoldoverride");

// Real concurrent revisions: same fenced source, two new commands from one inspected revision => exactly one writer.
var race1 = await PlanningCommand(PlanningKind.OverridePriority, PriorityBand.High); var race2 = race1 with { EventId = Guid.NewGuid(), PriorityOverride = PriorityBand.Low };
var racing = await Task.WhenAll(Plan(race1), Plan(race2)); Check(racing.Count(r => r.Issue is null) == 1 && racing.Count(r => r.Issue == OutcomePriorityIssue.RevisionConflict) == 1, "realconcurrentonewinner");
var replayRace = await Task.WhenAll(Plan(racing[0].Issue is null ? race1 : race2), Plan(racing[0].Issue is null ? race1 : race2)); Check(replayRace.All(r => r.AlreadyApplied), "concurrentexactreplay");

foreach (var authority in new[] { Consultant with { Authenticated = false }, Consultant with { Revoked = true }, Consultant with { AssignmentActive = false }, Consultant with { Categories = ["OPERATIONS"] }, Consultant with { Roles = [OutcomeRole.Auditor] }, Consultant with { ResourceState = OutcomeResourceState.Deleted } })
{
    rowsBefore = await Rows(); Check((await Plan(await PlanningCommand(PlanningKind.OverridePriority, PriorityBand.High), authority)).Issue == OutcomePriorityIssue.Denied, "actualhostauthoritydeny"); Check(await Rows() == rowsBefore, "denynowrites");
}
var export = new OutcomeExportAuthority("synthetic-auditor", true, true, false, true, OutcomeScope.Fixed, OutcomeRole.Auditor, ["SECURITY", "OPERATIONS"], OutcomeResourceState.Mutable, true, true, true);
var exported = await Tx((c, t) => store.VerifyLockedForExportInTransactionAsync(c, t, Run, export)); Check(exported.Issue is null && exported.Proof!.OutcomeLockDigest == locked1.Lock.ContentDigest, "genuineauditorexportproof");
Check(!OutcomePriorityCanonical.Json(exported.Proof).Contains("Fictional", StringComparison.Ordinal), "exportproofnobodytext");
foreach (var invalid in new[] { export with { TaskExportGrant = false }, export with { CustomerExportPolicy = false }, export with { ScopedAuditorExportGrant = false }, export with { Categories = ["OPERATIONS"] }, export with { Revoked = true }, export with { Role = OutcomeRole.PlatformSupport } }) Check((await Tx((c, t) => store.VerifyLockedForExportInTransactionAsync(c, t, Run, invalid))).Issue == OutcomePriorityIssue.Denied, "genuineexportdenials");

// A writer holding the real run fence updates the owning source; the reader must wait and see one post-write snapshot.
await using (var writer = await Open())
await using (var transaction = await writer.BeginTransactionAsync())
{
    await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(writer, transaction, OutcomeScope.Fixed.CustomerId, OutcomeScope.Fixed.ProjectId, OutcomeScope.Fixed.EnvironmentId, Run);
    var blockedRead = Planning();
    await Task.Delay(100);
    Check(!blockedRead.IsCompleted, "actualrunfenceblocksread");
    await using var write = new NpgsqlCommand("UPDATE public.op_test_sources SET source_json=@json WHERE run_id=@run", writer, transaction);
    write.Parameters.AddWithValue("json", OutcomePriorityCanonical.Json(Source(3))); write.Parameters.AddWithValue("run", Run);
    await write.ExecuteNonQueryAsync(); await transaction.CommitAsync();
    var coherent = await blockedRead;
    Check(coherent.Issue is null && coherent.Snapshot!.Source.SourceRevision == 3 && coherent.Snapshot.Entries.Single().EffortApproval == EffortApprovalState.Proposed, "postwritecoherentsource");
}
// Kill a real child after the module writes but before caller commit; PostgreSQL rollback must preserve complete rows.
rowsBefore = await Rows();
var crashStart = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
crashStart.ArgumentList.Add("--crash-uncommitted");
using (var child = Process.Start(crashStart)!)
{
    var ready = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
    Check(ready == "READY_UNCOMMITTED", "realchilduncommittedready");
    child.Kill(entireProcessTree: true); await child.WaitForExitAsync();
    Check(child.ExitCode != 0, "realchildkilled");
}
Check(await Rows() == rowsBefore, "realchildcrashrollbackbytes");
Check((await Planning()).Issue is null, "realchildrecoveryfencereacquired");

// Concurrent approved replacements cannot partially supersede or install two current approved versions.
Check((await Apply(await Command(OutcomeKind.CreateDraft, "access-governance", 5))).Issue is null, "draft5");
Check((await Apply(await Command(OutcomeKind.Review, "access-governance", 5))).Issue is null, "review5");
var nextApproval = await Command(OutcomeKind.Approve, "access-governance", 5);
var approvalRace = await Task.WhenAll(Apply(nextApproval, Approver), Apply(nextApproval with { EventId = Guid.NewGuid() }, Approver));
Check(approvalRace.Count(r => r.Issue is null) == 1 && approvalRace.Count(r => r.Issue == OutcomePriorityIssue.RevisionConflict) == 1, "registryreplacementonewinner");
var finalRegistry = await Registry(); Check(finalRegistry.Entries.Count(e => e.State == OutcomeState.CustomerApproved) == 1 && finalRegistry.HighWater.Single().HighestApprovedVersion == 5, "registryreplacementatomic");
var faultPlanning = new SyntheticOutcomePriorityStore(OutcomeScope.Fixed, async (c, t, run, _, ct) =>
{
    await using var q = new NpgsqlCommand("SELECT source_json FROM public.op_test_sources WHERE run_id=@run", c, t); q.Parameters.AddWithValue("run", run);
    return new(null, OutcomePriorityCanonical.Parse<Phase1BPlanningSource>((string)(await q.ExecuteScalarAsync(ct))!));
}, new ThrowObserver());
rowsBefore = await Rows();
Check((await Plan(await PlanningCommand(PlanningKind.OverridePriority, PriorityBand.Immediate), target: faultPlanning)).Issue == OutcomePriorityIssue.IntegrityMismatch, "planningfinalwritefault");
Check(await Rows() == rowsBefore, "planningpartialwritesrolledback");

// Corruption tests preserve and restore exact accepted bytes; no regenerating source from helpers.
var lockJson = await Scalar<string>("SELECT lock_json FROM synthetic_outcome_priority.run_locks WHERE run_id=@run", ("run", Run));
await Db("UPDATE synthetic_outcome_priority.run_locks SET lock_json='{}' WHERE run_id=@run", ("run", Run));
Check((await Tx((c, t) => store.ReadLockedAsync(c, t, Run, Consultant))).Issue == OutcomePriorityIssue.IntegrityMismatch, "corruptlocknocompoundfallback");
Check((await Tx((c, t) => store.VerifyLockedForExportInTransactionAsync(c, t, Run, export))).Issue == OutcomePriorityIssue.IntegrityMismatch, "exportcorruptlockdeny");
await Db("UPDATE synthetic_outcome_priority.run_locks SET lock_json=@json WHERE run_id=@run", ("json", lockJson!), ("run", Run));
var savedReceipt = await Scalar<string>("SELECT receipt_json FROM synthetic_outcome_priority.receipts WHERE event_id=@event", ("event", approval4.EventId));
await Db("UPDATE synthetic_outcome_priority.receipts SET receipt_json='{}' WHERE event_id=@event", ("event", approval4.EventId));
Check((await Tx((c, t) => store.ReadRegistryAsync(c, t, Consultant))).Issue == OutcomePriorityIssue.IntegrityMismatch, "missingreceiptfailsregistry");
Check((await Tx((c, t) => store.ReadLockedAsync(c, t, Run, Consultant))).Issue is null, "historicalselfcontainedcurrentregistryunavailable");
await Db("UPDATE synthetic_outcome_priority.receipts SET receipt_json=@json WHERE event_id=@event", ("json", savedReceipt!), ("event", approval4.EventId));
await Db("ALTER TABLE synthetic_outcome_priority.planning_current ADD COLUMN unexpected text");
Check((await Tx((c, t) => store.ReadPlanningAsync(c, t, Run, Consultant))).Issue == OutcomePriorityIssue.MigrationDrift, "schemadriftdeny");
await Db("ALTER TABLE synthetic_outcome_priority.planning_current DROP COLUMN unexpected");
// Dropped-column catalog history remains drift: initialization must refuse rather than silently repair.
await using (var c = await Open()) { try { await store.InitializeAsync(c); Check(false, "initializationdrift"); } catch (OutcomePriorityMigrationException) { Check(true, "initializationrefusesdrift"); } }
Console.WriteLine($"PASS PostgreSQL: {Assertions} lifecycle/lock/highwater/planning/export/concurrency/replay/corruption assertions; dedicated {database}");

async Task<T> Tx<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> action)
{
    await using var c = await Open(); await using var t = await c.BeginTransactionAsync();
    var result = await action(c, t);
    var issue = result switch { OutcomeApplyResult r => r.Issue, PlanningApplyResult r => r.Issue, OutcomeLockResult r => r.Issue, OutcomeRegistryResult r => r.Issue, PlanningReadResult r => r.Issue, OutcomeExportProofResult r => r.Issue, _ => null };
    if (issue is null) await t.CommitAsync(); else await t.RollbackAsync();
    return result;
}
async Task<NpgsqlConnection> Open() { var c = new NpgsqlConnection(connectionString); await c.OpenAsync(); return c; }
async Task Db(string sql, params (string Name, object Value)[] parameters)
{ await using var c = await Open(); await using var q = new NpgsqlCommand(sql, c); foreach (var p in parameters) q.Parameters.AddWithValue(p.Name, p.Value); await q.ExecuteNonQueryAsync(); }
async Task<T?> Scalar<T>(string sql, params (string Name, object Value)[] parameters)
{ await using var c = await Open(); await using var q = new NpgsqlCommand(sql, c); foreach (var p in parameters) q.Parameters.AddWithValue(p.Name, p.Value); return (T?)await q.ExecuteScalarAsync(); }
async Task<string> Rows()
{
    var rows = new List<string>();
    foreach (var table in new[] { "outcome_versions", "outcome_events", "outcome_current", "outcome_highwater", "registry_current", "run_locks", "planning_sources", "planning_events", "planning_current", "receipts" }) rows.Add(await Scalar<string>("SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text,'[]') FROM synthetic_outcome_priority." + table + " t") ?? "[]");
    return string.Join("\n", rows);
}
internal sealed class ThrowObserver : IOutcomePriorityWriteObserver
{ public Task BeforeWriteAsync(string operation, Guid eventId, CancellationToken ct) => throw new InvalidOperationException("Injected owned-write failure."); }

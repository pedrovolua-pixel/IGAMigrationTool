using System.Collections.Immutable;
using FindingReview;
using Npgsql;

var checks = 0;
var scope = SyntheticReviewScope.Fixed;
var authority = new SyntheticReviewAuthority("synthetic-consultant", true, true, false, true, scope,
    [SyntheticReviewRole.Consultant], ["SECURITY", "OPERATIONS"], Enum.GetValues<SyntheticReviewAction>().ToImmutableArray());
var finding = new SyntheticFindingCurrent(new string('a', 64), "SECURITY", 0, SyntheticFindingState.Proposed, "Original fixture title", "");
foreach (var action in Enum.GetValues<SyntheticReviewAction>())
{
    Check($"consultant {action}", SyntheticReviewPolicy.Authorize(authority, scope, "SECURITY", action, SyntheticReviewResourceState.Mutable) is null);
    Check($"qualified reviewer explicitly granted {action}", SyntheticReviewPolicy.Authorize(authority with { Roles = [SyntheticReviewRole.QualifiedReviewer] }, scope, "SECURITY", action, SyntheticReviewResourceState.Mutable) is null);
    foreach (var wrong in new[]
    {
        authority with { Authenticated = false }, authority with { Active = false }, authority with { Revoked = true },
        authority with { AssignmentActive = false }, authority with { AssignedScope = scope with { CustomerId = "wrong" } },
        authority with { AssignedScope = scope with { ProjectId = "wrong" } }, authority with { AssignedScope = scope with { EnvironmentId = "wrong" } },
        authority with { Roles = [] }, authority with { Roles = [SyntheticReviewRole.Auditor] }, authority with { Roles = [SyntheticReviewRole.Executive] },
        authority with { Roles = [SyntheticReviewRole.CustomerRiskOwner] }, authority with { Roles = [SyntheticReviewRole.PlatformSupport] },
        authority with { Categories = ["OPERATIONS"] }, authority with { Actions = [] }, authority with { ActorId = " " }
    }) Check($"deny identity/category/action {action}", SyntheticReviewPolicy.Authorize(wrong, scope, "SECURITY", action, SyntheticReviewResourceState.Mutable) == SyntheticReviewIssue.Denied);
    foreach (var resource in Enum.GetValues<SyntheticReviewResourceState>().Where(state => state != SyntheticReviewResourceState.Mutable))
        Check($"deny blocked resource {resource}/{action}", SyntheticReviewPolicy.Authorize(authority, scope, "SECURITY", action, resource) == SyntheticReviewIssue.Denied);
    Check($"wrong resolved scope {action}", SyntheticReviewPolicy.Authorize(authority, scope with { ProjectId = "wrong" }, "SECURITY", action, SyntheticReviewResourceState.Mutable) == SyntheticReviewIssue.WrongScope);
}
foreach (var kind in Enum.GetValues<SyntheticReviewEventKind>())
{
    var command = Command(kind);
    Check($"valid bounded command {kind}", SyntheticReviewPolicy.ValidateCommand(command) is null);
    Check($"proposed transition {kind}", SyntheticReviewPolicy.ValidateTransition(finding, command) is null);
    Check($"revision increments {kind}", SyntheticReviewPolicy.Next(finding, command).Revision == 1);
    foreach (var state in Enum.GetValues<SyntheticFindingState>().Where(state => state != SyntheticFindingState.Proposed))
        Check($"limited transition {state}/{kind}", SyntheticReviewPolicy.ValidateTransition(finding with { State = state }, command) ==
            (kind is SyntheticReviewEventKind.Confirm or SyntheticReviewEventKind.Reject or SyntheticReviewEventKind.Defer ? SyntheticReviewIssue.InvalidState : null));
}
foreach (var invalid in new[]
{
    new SyntheticReviewCommand(Guid.Empty, 0, SyntheticReviewEventKind.Confirm), new(Guid.NewGuid(), -1, SyntheticReviewEventKind.Confirm),
    new(Guid.NewGuid(), long.MaxValue, SyntheticReviewEventKind.Confirm), new(Guid.NewGuid(), 0, (SyntheticReviewEventKind)999),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject), new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, Reason: " "),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, Reason: new string('r', 2001)),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm, Text: "extra field"),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: new string('c', 2001)),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: " "),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation), new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, Title: " "),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, Title: new string('t', 251)),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, BusinessContext: new string('b', 2001)),
    new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Defer, BusinessContext: "extra field")
}) Check("invalid/unsupported command denied", SyntheticReviewPolicy.ValidateCommand(invalid) == SyntheticReviewIssue.InvalidInput);
Check("maximum text boundary", SyntheticReviewPolicy.ValidateCommand(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Comment, Text: new string('c', 2000))) is null);
Check("maximum edit boundary", SyntheticReviewPolicy.ValidateCommand(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, Title: new string('t', 250), BusinessContext: new string('b', 2000))) is null);
Check("empty business context can clear", SyntheticReviewPolicy.ValidateCommand(new(Guid.NewGuid(), 0, SyntheticReviewEventKind.EditPresentation, BusinessContext: "")) is null);
foreach (var connection in new[] { "Host=remote;Database=iga_synthetic_r5", "Host=127.0.0.1;Database=production", "Host=127.0.0.1,remote;Database=iga_synthetic_r5" })
{
    try { _ = new SyntheticReviewStore(connection, scope); throw new Exception("Connection guard accepted invalid connection."); }
    catch (ArgumentException) { checks++; }
}
Console.WriteLine($"{checks} portable synthetic review policy checks passed.");
var portable = checks;
var database = Environment.GetEnvironmentVariable("IGA_R5_DATABASE");
if (database is null)
{
    Console.WriteLine("Actual PostgreSQL review checks NOT VERIFIED; set IGA_R5_DATABASE to a precreated dedicated synthetic database.");
    return;
}
var store = new SyntheticReviewStore(database, scope);
await store.InitializeAsync();
await store.InitializeAsync();
var seed = Seed(Guid.NewGuid());
var firstId = seed.Findings[0].FindingId;
var secondId = seed.Findings[1].FindingId;
Check("seed committed", (await store.SeedAsync(seed, authority)).Succeeded);
Check("exact seed replay", (await store.SeedAsync(seed with { Findings = seed.Findings.Reverse().ToImmutableArray() }, authority)).AlreadyApplied);
Check("seed mismatch denied", (await store.SeedAsync(seed with { AnalysisDigest = new string('4', 64) }, authority)).Issue == SyntheticReviewIssue.SeedConflict);
var initial = (await store.ReadAsync(scope, seed.RunId, authority)).Snapshot!;
Check("initial immutable originals", initial.Findings.Length == 2 && initial.Findings.All(item => item.Current.Revision == 0 && item.History.IsEmpty));
Check("initial snapshot digest", initial.SnapshotDigest == SyntheticReviewDigest.Compute(initial with { SnapshotDigest = "" }));
var defer = new SyntheticReviewCommand(Guid.NewGuid(), 0, SyntheticReviewEventKind.Defer, Reason: "Synthetic review pending context.");
var deferred = await store.ApplyAsync(scope, seed.RunId, firstId, authority, defer);
Check("defer retains reviewed disposition", deferred.Succeeded && deferred.Outcome!.State == SyntheticFindingState.Deferred && deferred.Outcome.Revision == 1);
Check("comment appends inert content", (await store.ApplyAsync(scope, seed.RunId, firstId, authority,
    new(Guid.NewGuid(), 1, SyntheticReviewEventKind.Comment, Text: "<script>inert fixture text</script>"))).Succeeded);
Check("edit preserves generated original", (await store.ApplyAsync(scope, seed.RunId, firstId, authority,
    new(Guid.NewGuid(), 2, SyntheticReviewEventKind.EditPresentation, Title: "Reviewed fixture presentation", BusinessContext: "Fictional context"))).Succeeded);
var after = (await store.ReadAsync(scope, seed.RunId, authority)).Snapshot!;
var reviewed = after.Findings.Single(item => item.Seed.FindingId == firstId);
Check("original title/digests unchanged", reviewed.Seed.OriginalTitle == "Original fixture title" && reviewed.Seed.OriginalDigests.SequenceEqual(seed.Findings[0].OriginalDigests));
Check("history and current coherent", reviewed.History.Length == 3 && reviewed.Current.Revision == 3 && reviewed.Current.PresentationTitle == "Reviewed fixture presentation" && reviewed.Current.BusinessContext == "Fictional context");
Check("actor/database UTC history", reviewed.History.All(item => item.ActorId == authority.ActorId && item.ActorRoles.SequenceEqual(authority.Roles) && item.RecordedAtUtc.Offset == TimeSpan.Zero));
var replay = await store.ApplyAsync(scope, seed.RunId, firstId, authority, defer);
Check("replay returns original outcome after later edits", replay.Succeeded && replay.AlreadyApplied && replay.Outcome == deferred.Outcome && replay.Outcome!.Revision == 1);
Check("changed replay conflicts", (await store.ApplyAsync(scope, seed.RunId, firstId, authority, defer with { Reason = "changed" })).Issue == SyntheticReviewIssue.EventConflict);
Check("another actor cannot borrow replay", (await store.ApplyAsync(scope, seed.RunId, firstId, authority with { ActorId = "synthetic-other" }, defer)).Issue == SyntheticReviewIssue.EventConflict);
Check("stale revision precedes now-invalid transition", (await store.ApplyAsync(scope, seed.RunId, firstId, authority, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm))).Issue == SyntheticReviewIssue.RevisionConflict);
Check("unsupported repeated transition denied", (await store.ApplyAsync(scope, seed.RunId, firstId, authority, new(Guid.NewGuid(), 3, SyntheticReviewEventKind.Confirm))).Issue == SyntheticReviewIssue.InvalidState);
Check("conflicts have no partial append", (await store.ReadAsync(scope, seed.RunId, authority)).Snapshot!.SnapshotDigest == after.SnapshotDigest);
var competitors = await Task.WhenAll(
    store.ApplyAsync(scope, seed.RunId, secondId, authority, new(defer.EventId, 0, SyntheticReviewEventKind.Confirm)),
    store.ApplyAsync(scope, seed.RunId, secondId, authority, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, Reason: "Synthetic false positive")));
Check("concurrent writers exactly one commit", competitors.Count(result => result.Succeeded) == 1 && competitors.Count(result => result.Issue == SyntheticReviewIssue.RevisionConflict) == 1);
var fresh = new SyntheticReviewStore(database, scope);
await fresh.InitializeAsync();
var latest = (await fresh.ReadAsync(scope, seed.RunId, authority)).Snapshot!;
Check("reload and whole-run history", latest.Findings.Sum(item => item.History.Length) == 4 && latest.Findings.Single(item => item.Seed.FindingId == secondId).Current.Revision == 1);
Check("snapshot digest changes with current/history", latest.SnapshotDigest != after.SnapshotDigest);
Check("read denied missing category", (await fresh.ReadAsync(scope, seed.RunId, authority with { Categories = ["SECURITY"] })).Issue == SyntheticReviewIssue.Denied);
Check("wrong scope denied", (await fresh.ReadAsync(scope with { CustomerId = "other" }, seed.RunId, authority)).Issue == SyntheticReviewIssue.WrongScope);
Check("revoked actor denied on replay", (await fresh.ApplyAsync(scope, seed.RunId, firstId, authority with { Revoked = true }, defer)).Issue == SyntheticReviewIssue.Denied);
Check("wrong run finding denied", (await fresh.ApplyAsync(scope, Guid.NewGuid(), firstId, authority, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Confirm))).Issue == SyntheticReviewIssue.NotFound);
var failing = new SyntheticReviewStore(database, scope, new FailingObserver());
try { await failing.ApplyAsync(scope, seed.RunId, firstId, authority, new(Guid.NewGuid(), 3, SyntheticReviewEventKind.Comment, Text: "roll back")); throw new Exception("Observer failed to throw."); }
catch (TestRollbackException) { checks++; }
Check("observer rollback append and current", (await fresh.ReadAsync(scope, seed.RunId, authority)).Snapshot!.SnapshotDigest == latest.SnapshotDigest);
var rollbackSeed = Seed(Guid.NewGuid());
try { await failing.SeedAsync(rollbackSeed, authority); throw new Exception("Seed observer failed to throw."); }
catch (TestRollbackException) { checks++; }
Check("seed rollback all rows", (await fresh.ReadAsync(scope, rollbackSeed.RunId, authority)).Issue == SyntheticReviewIssue.NotFound);
await using var raw = new NpgsqlConnection(database);
await raw.OpenAsync();
foreach (var sql in new[]
{
    "UPDATE synthetic_review.run_seeds SET seed_digest=seed_digest WHERE run_id=@run",
    "DELETE FROM synthetic_review.finding_seeds WHERE run_id=@run",
    "UPDATE synthetic_review.events SET event_json=event_json WHERE run_id=@run",
    "DELETE FROM synthetic_review.events WHERE run_id=@run",
    "TRUNCATE synthetic_review.events",
    "UPDATE synthetic_review.finding_current SET revision=revision WHERE run_id=@run",
    "UPDATE synthetic_review.data_plane_scope SET customer_id=customer_id",
    "UPDATE synthetic_review.schema_migrations SET digest=digest"
})
{
    await using var command = new NpgsqlCommand(sql, raw);
    command.Parameters.AddWithValue("run", seed.RunId);
    try { await command.ExecuteNonQueryAsync(); throw new Exception("Raw immutable rewrite accepted."); }
    catch (PostgresException exception) when (exception.SqlState == "55000") { checks++; }
}
Check("raw rewrite denials preserve snapshot", (await fresh.ReadAsync(scope, seed.RunId, authority)).Snapshot!.SnapshotDigest == latest.SnapshotDigest);
var malformed = Seed(Guid.NewGuid());
await using (var command = new NpgsqlCommand("INSERT INTO synthetic_review.run_seeds VALUES (@run,@customer,@project,@environment,@json,@digest,clock_timestamp())", raw))
{
    command.Parameters.AddWithValue("run", malformed.RunId);
    command.Parameters.AddWithValue("customer", scope.CustomerId);
    command.Parameters.AddWithValue("project", scope.ProjectId);
    command.Parameters.AddWithValue("environment", scope.EnvironmentId);
    command.Parameters.AddWithValue("json", "{invalid fixture JSON");
    command.Parameters.AddWithValue("digest", new string('0', 64));
    await command.ExecuteNonQueryAsync();
}
Check("corrupt pre-existing seed typed denial", (await fresh.SeedAsync(malformed, authority)).Issue == SyntheticReviewIssue.IntegrityMismatch);
Check("corrupt seed read typed denial", (await fresh.ReadAsync(scope, malformed.RunId, authority)).Issue == SyntheticReviewIssue.IntegrityMismatch);
await using (var alter = new NpgsqlCommand("ALTER TABLE synthetic_review.finding_current ADD COLUMN synthetic_test_drift text", raw)) await alter.ExecuteNonQueryAsync();
try
{
    Check("operation schema drift denied", (await fresh.ReadAsync(scope, seed.RunId, authority)).Issue == SyntheticReviewIssue.MigrationDrift);
    try { await fresh.InitializeAsync(); throw new Exception("Schema drift initialization accepted."); }
    catch (SyntheticReviewMigrationException) { checks++; }
}
finally
{
    await using var repair = new NpgsqlCommand("ALTER TABLE synthetic_review.finding_current DROP COLUMN synthetic_test_drift", raw);
    await repair.ExecuteNonQueryAsync();
}
await fresh.InitializeAsync();
Check("schema restored and retained history", (await fresh.ReadAsync(scope, seed.RunId, authority)).Snapshot!.SnapshotDigest == latest.SnapshotDigest);
Console.WriteLine($"{checks - portable} actual PostgreSQL synthetic review checks passed; {checks} total.");

void Check(string name, bool condition) { if (!condition) throw new Exception(name); checks++; }
SyntheticReviewCommand Command(SyntheticReviewEventKind kind) => kind switch
{
    SyntheticReviewEventKind.Reject => new(Guid.NewGuid(), 0, kind, Reason: "Synthetic rejection"),
    SyntheticReviewEventKind.Comment => new(Guid.NewGuid(), 0, kind, Text: "Inert synthetic comment"),
    SyntheticReviewEventKind.EditPresentation => new(Guid.NewGuid(), 0, kind, Title: "Presentation", BusinessContext: "Context"),
    _ => new(Guid.NewGuid(), 0, kind)
};
SyntheticReviewRunSeed Seed(Guid runId) => new(scope, runId, new string('1', 64), new string('2', 64), SyntheticReviewResourceState.Mutable,
[
    new(new string('a', 64), "SECURITY", SyntheticFindingState.Proposed, "Original fixture title", [new string('b', 64)],
        [new(new string('c', 64), "fixture-object-1", "fixture-rule-1", "fixture-rule-v1", new string('b', 64))]),
    new(new string('d', 64), "OPERATIONS", SyntheticFindingState.Proposed, "Another original fixture title", [new string('e', 64)],
        [new(new string('f', 64), "fixture-object-2", "fixture-rule-2", "fixture-rule-v1", new string('e', 64))])
]);
sealed class TestRollbackException : Exception;
sealed class FailingObserver : ISyntheticReviewCommitObserver
{
    public Task BeforeCommitAsync(string operation, Guid runId, Guid? eventId, CancellationToken cancellationToken) => throw new TestRollbackException();
}

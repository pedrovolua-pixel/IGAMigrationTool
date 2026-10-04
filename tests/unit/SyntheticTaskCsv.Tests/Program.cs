using System.Collections.Immutable;
using System.Text;
using SyntheticTaskCsv;

var passed = 0;
void Assert(bool condition, string label) { if (!condition) throw new Exception(label); passed++; }
var path = Path.Combine(AppContext.BaseDirectory, "Fixtures");
var golden = File.ReadAllText(Path.Combine(path, "golden-envelope.json"));
var parsed = CsvCodec.Parse(golden); Assert(parsed.Succeeded, "independent closed envelope accepted");
var envelope = parsed.Envelope!;
var expected = File.ReadAllBytes(Path.Combine(path, "golden-one-row.csv"));
Assert(CsvCodec.Render(envelope).AsSpan().SequenceEqual(expected), "full literal bytes and ordered cells");
var digests = File.ReadAllLines(Path.Combine(path, "golden-digests.txt"));
Assert(envelope.SnapshotDigest == digests[0], "independent canonical snapshot digest");
var verification = CsvCodec.Verify(envelope, expected, Guid.NewGuid());
Assert(verification.Succeeded && verification.Receipt!.OutputSha256 == digests[1] && verification.Receipt.SnapshotDigest == digests[0] && digests[0] != digests[1], "separate input/output digests and full parity receipt");
var empty = CsvCodec.Freeze(envelope with { Rows = [], SelectedAttestations = [] });
var header = CsvCodec.Render(empty.Envelope!);
Assert(header.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(path, "golden-header.csv"))) && header.Length == 283 && CsvCanonical.Hash(header) == "2e1c6fedadd38cb870c4d6ef25dbd44f806fdc16f6c10ed3c20293c8a9f4b603", "header-only independent golden");
foreach (var (input, output) in new[] { ("=1+1", "\"'=1+1\""), (" +2", "\"' +2\""), ("\u00a0-3", "\"'\u00a0-3\""), ("\u2003@SUM(A1)", "\"'\u2003@SUM(A1)\""), ("\"quoted\", comma", "\"\"\"quoted\"\", comma\"") })
    Assert(CsvCodec.QuoteCell(input) == output, "literal formula/quote vector " + input);
foreach (var invalid in new[] { "\t=1", "bad\r", "bad\n", "bad\0", "\ud800", new string('é', 1025) })
    Assert(!CsvCodec.ValidCell(invalid), "unsafe cells denied");
foreach (var attack in new[] { golden.Replace("\"schemaVersion\":\"synthetic-task-csv-envelope-v1\"", "\"schemaVersion\":\"wrong\""), golden.Replace("\"assigneeId\":", "\"rawComment\":\"secret\",\"assigneeId\":"), golden.Replace("\"taskRevision\":7", "\"taskRevision\":8"), golden.Replace("\"taskRevision\":7", "\"taskRevision\":7,\"taskRevision\":7"), golden.Replace("localhost:5183", "attacker.invalid"), golden.Replace("synthetic-phase1b-input-v1", "other"), golden.Replace("\"eventId\":null,", ""), golden.Replace("\"assigneeId\":", "\"AssigneeId\":") })
    Assert(!CsvCodec.Parse(attack).Succeeded, "tamper/unknown/duplicate/trusted-origin/version denied");
Assert(!CsvCodec.Parse(new string(' ', CsvCodec.MaximumInputBytes + 1)).Succeeded, "input byte bound");
Assert(!CsvCodec.Freeze(envelope with { Rows = default }).Succeeded, "missing rows distinct from empty");
Assert(!CsvCodec.Freeze(envelope with { SelectedAttestations = [] }).Succeeded, "exact three selected attestations required");
Assert(!CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0], envelope.Rows[0]] }).Succeeded, "duplicate task denied");
var mutated = expected.ToArray(); mutated[^4] ^= 1;
Assert(!CsvCodec.Verify(envelope, mutated, Guid.NewGuid()).Succeeded, "same-length CSV cell tamper denied");
Assert(!CsvCodec.Verify(envelope, expected.AsSpan(0, expected.Length - 1), Guid.NewGuid()).Succeeded, "truncated output denied");
Assert(!CsvCodec.Verify(envelope, expected, Guid.Empty).Succeeded, "receipt requires request identity");
foreach (var state in new[] { "Planned", "InProgress", "Completed", "Cancelled" })
    Assert(CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0] with { TaskStatus = state }] }).Succeeded, "all task states retained");
foreach (var freshness in new[] { "CurrentPlan", "NeedsReconfirmation" })
    Assert(CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0] with { PlanFreshness = freshness, PlannedSourceDigest = freshness == "CurrentPlan" ? envelope.Rows[0].CurrentSourceDigest : envelope.Rows[0].PlannedSourceDigest }] }).Succeeded, "current and stale metadata retained");
Assert(!CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0] with { PlanFreshness = "SourceUnavailable" }] }).Succeeded, "unavailable source denies whole export");

Assert(!CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0] with { CreatedAtUtc = "2026-10-01T12:00:00+01:00" }] }).Succeeded, "exact UTC timestamp only");
Assert(!CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0] with { TaskRevision = CsvCodec.MaximumRevision + 1 }] }).Succeeded, "safe revision bound");
Assert(!CsvCodec.Freeze(envelope with { SelectedAttestations = envelope.SelectedAttestations.SetItem(0, envelope.SelectedAttestations[0] with { Revision = 1 }) }).Succeeded,
    "complete event proof required for positive artifact revision");
Assert(!CsvCodec.Freeze(envelope with { Rows = Enumerable.Repeat(envelope.Rows[0], CsvCodec.MaximumRows + 1).ToImmutableArray() }).Succeeded, "row count bound");
Assert(CsvCodec.ValidCell(new string('é', 1024)), "exact UTF8 cell byte boundary");
Assert(!CsvCodec.Freeze(envelope with { Rows = [envelope.Rows[0] with { PlanFreshness = "CurrentPlan" }] }).Succeeded, "current plan requires same source digest");
Assert(!CsvCodec.Freeze(envelope with { CurrentSourceBinding = envelope.CurrentSourceBinding with { FindingRevisions = [] } }).Succeeded, "row finding must exist in captured source vector");
Console.WriteLine($"PASS {passed} CSV assertions; independent canonical/full literal/header/formula/cell/tamper/limit/state oracles.");

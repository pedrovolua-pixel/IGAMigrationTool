using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportPublication;
using ReportPublication.PostgreSql;

internal static class AdapterPortableChecks
{
    internal sealed record Result(string Name, bool Passed, string? FailureType);
    internal static async Task<IReadOnlyList<Result>> RunAsync()
    {
        var results = new List<Result>();
        void Check(string name, Action test)
        {
            try { test(); results.Add(new(name, true, null)); }
            catch (Exception ex) { results.Add(new(name, false, ex.GetType().Name)); }
        }
        async Task CheckAsync(string name, Func<Task> test)
        {
            try { await test(); results.Add(new(name, true, null)); }
            catch (Exception ex) { results.Add(new(name, false, ex.GetType().Name)); }
        }
        var parsers = new Dictionary<string, Func<byte[], byte[]>>(StringComparer.Ordinal)
        {
            ["manifest"] = bytes => NativePublicationCanonicalV1.ManifestBytes(NativeControlReaderV1.Manifest(bytes)),
            ["receipt"] = bytes => NativePublicationCanonicalV1.PublicationReceiptBytes(NativeControlReaderV1.PublicationReceipt(bytes)),
            ["read-receipt"] = bytes => NativePublicationCanonicalV1.ReadReceiptBytes(NativeControlReaderV1.ReadReceipt(bytes)),
            ["publish-event"] = bytes => NativePublicationCanonicalV1.AuditEventBytes(NativeControlReaderV1.Audit(bytes)),
            ["read-event"] = bytes => NativePublicationCanonicalV1.AuditEventBytes(NativeControlReaderV1.Audit(bytes))
        };
        foreach (var bundle in new[] { "completed", "warned", "source-limitation" })
        {
            foreach (var parser in parsers)
            {
                var bytes = Original(bundle + "." + parser.Key + ".json");
                Check("original-typed-control:" + bundle + ":" + parser.Key, () => Require(parser.Value(bytes).SequenceEqual(bytes)));
                Check("input-copy:" + bundle + ":" + parser.Key, () =>
                {
                    var supplied = bytes.ToArray(); var actual = parser.Value(supplied); Array.Clear(supplied); Require(actual.SequenceEqual(bytes));
                });
                Refusal("root-duplicate:" + bundle + ":" + parser.Key, bytes, parser.Value, DuplicateRoot);
                Refusal("alias-duplicate:" + bundle + ":" + parser.Key, bytes, parser.Value, AliasDuplicateRoot);
                Refusal("extra-field:" + bundle + ":" + parser.Key, bytes, parser.Value, b => Encoding.UTF8.GetBytes("{\"rawSql\":null," + Encoding.UTF8.GetString(b)[1..]));
                Refusal("trailing-newline:" + bundle + ":" + parser.Key, bytes, parser.Value, b => b.Concat(new byte[] { 10 }).ToArray());
                Refusal("BOM:" + bundle + ":" + parser.Key, bytes, parser.Value, b => new byte[] { 239, 187, 191 }.Concat(b).ToArray());
                Refusal("raw-NUL:" + bundle + ":" + parser.Key, bytes, parser.Value, b => b.Concat(new byte[] { 0 }).ToArray());
                Refusal("invalid-UTF8:" + bundle + ":" + parser.Key, bytes, parser.Value, b => b.Concat(new byte[] { 255 }).ToArray());
                using var document = JsonDocument.Parse(bytes);
                var root = document.RootElement;
                var nested = root.EnumerateObject().First(x => x.Value.ValueKind == JsonValueKind.Object);
                Refusal("nested-duplicate:" + bundle + ":" + parser.Key, bytes, parser.Value,
                    b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace(nested.Value.GetRawText(), Encoding.UTF8.GetString(DuplicateRoot(Encoding.UTF8.GetBytes(nested.Value.GetRawText()))), StringComparison.Ordinal)));
                var counter = root.EnumerateObject().First(x => x.Name is "runRevision" or "expectedRunRevision" or "sequence" || x.Name == "actor").Name;
                if (counter == "actor")
                {
                    Refusal("actor-counter-number:" + bundle + ":" + parser.Key, bytes, parser.Value,
                        b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"securityVersion\":\"3\"", "\"securityVersion\":3", StringComparison.Ordinal)));
                }
                else
                {
                    var value = root.GetProperty(counter).GetString()!;
                    Refusal("counter-padded:" + bundle + ":" + parser.Key, bytes, parser.Value,
                        b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"" + counter + "\":\"" + value + "\"", "\"" + counter + "\":\"0" + value + "\"", StringComparison.Ordinal)));
                }
                var schema = root.GetProperty("schemaVersion").GetString()!;
                Refusal("schema-alias:" + bundle + ":" + parser.Key, bytes, parser.Value,
                    b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace(schema, schema.ToUpperInvariant(), StringComparison.Ordinal)));
                var time = root.EnumerateObject().First(x => x.Name.EndsWith("AtUtc", StringComparison.Ordinal));
                Refusal("UTC-precision:" + bundle + ":" + parser.Key, bytes, parser.Value,
                    b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace(time.Value.GetString()!, time.Value.GetString()!.Replace(".0000000Z", ".000000Z", StringComparison.Ordinal), StringComparison.Ordinal)));
            }
            var sourceBytes = Original(bundle + ".source.json"); var sourceNode = JsonNode.Parse(sourceBytes)!;
            var metadata = FixtureSourceDecoder.MetadataFor(sourceNode, FrozenFixtureLoader.Source(sourceNode));
            Check("sourceMetadata-original:" + bundle, () =>
            {
                var typed = NativeControlReaderV1.SourceMetadata(metadata);
                Require(typed.Scope == FrozenFixtureLoader.Source(sourceNode).Projection.Scope && typed.RunRevision == 9
                    && typed.SourceDigest == NativePublicationCanonicalV1.Hash(sourceBytes) && typed.RequiredFields.Length == 17);
            });
            Check("sourceMetadata-owned:" + bundle, () =>
            {
                var supplied = metadata.ToArray(); var typed = NativeControlReaderV1.SourceMetadata(supplied); Array.Clear(supplied);
                Require(typed.SourceDigest == NativePublicationCanonicalV1.Hash(sourceBytes) && typed.ProtectedReferences.Length == 1);
            });
            void MetaRefuse(string name, Action<JsonNode> mutate)
            {
                Check("sourceMetadata-refusal:" + bundle + ":" + name, () =>
                {
                    var changed = JsonNode.Parse(metadata)!; mutate(changed);
                    Deny(() => NativeControlReaderV1.SourceMetadata(FixtureSourceDecoder.CanonicalFixtureControl(changed)));
                });
            }
            MetaRefuse("missing-field-registry", n => n["requiredFields"]!.AsArray().RemoveAt(0));
            MetaRefuse("duplicate-field-registry", n => n["requiredFields"]!.AsArray().Add(n["requiredFields"]![0]!.DeepClone()));
            MetaRefuse("unsorted-field-registry", n => { var first = n["requiredFields"]![0]!.DeepClone(); n["requiredFields"]!.AsArray().RemoveAt(0); n["requiredFields"]!.AsArray().Add(first); });
            MetaRefuse("zero-run-revision", n => n["runRevision"] = "0");
            MetaRefuse("digest-uppercase", n => n["sourceDigest"] = new string('A', 64));
            MetaRefuse("unknown-field", n => n["rawSql"] = null);
            MetaRefuse("duplicate-provenance", n => n["provenance"]!.AsArray().Add(n["provenance"]![0]!.DeepClone()));
            MetaRefuse("duplicate-reference", n => n["protectedReferences"]!.AsArray().Add(n["protectedReferences"]![0]!.DeepClone()));
            MetaRefuse("desired-pair-missing", n => n["inputs"]!["desiredOutcomeVersion"] = "profile-v1");
            MetaRefuse("unpaired-retention", n => n["retention"]!["expiresAtUtc"] = "2026-10-02T12:00:00.0000000Z");
            MetaRefuse("unrecognized-warning", n => n["warnings"]!.AsArray().Add(new JsonObject { ["kind"] = "Alien", ["recordId"] = "00000000-0000-4000-8000-000000000031", ["category"] = "controls" }));
            MetaRefuse("unknown-protected-reference-field", n => n["protectedReferences"]![0]!["locator"] = "FICTIONAL_LOCATOR_SENTINEL");
        }
        Check("anonymous-audit-original", () => Require(NativePublicationCanonicalV1.AuditEventBytes(NativeControlReaderV1.Audit(Original("anonymous-denial.audit.json"))).SequenceEqual(Original("anonymous-denial.audit.json"))));
        Check("control-oversize-refusal", () => Deny(() => NativeControlReaderV1.Manifest(new byte[1024 * 1024 + 1])));
        var origin = DateTimeOffset.Parse("2026-10-03T12:00:00.0000000Z", System.Globalization.CultureInfo.InvariantCulture);
        Check("lease-minus-one-exact-plus-one", () =>
        {
            var clock = new FixtureClock(origin); using var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(2), default);
            clock.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1)); lease.Check(); Require(!lease.Token.IsCancellationRequested);
            clock.Advance(TimeSpan.FromTicks(1)); Require(lease.Token.IsCancellationRequested); Canceled(lease.Check);
            clock.Advance(TimeSpan.FromTicks(1)); Canceled(lease.Check);
        });
        Check("lease-narrow-original-monotonic-origin", () =>
        {
            var clock = new FixtureClock(origin); using var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(10), default);
            clock.Advance(TimeSpan.FromSeconds(3)); lease.Narrow(origin + TimeSpan.FromSeconds(5));
            lease.Narrow(origin + TimeSpan.FromSeconds(30)); Require(lease.DeadlineUtc == origin + TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(2)); Require(lease.Token.IsCancellationRequested); Canceled(lease.Check);
        });
        Check("lease-UTC-rollback-monotonic-expiry", () =>
        {
            var clock = new FixtureClock(origin); using var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(2), default);
            clock.Advance(TimeSpan.FromSeconds(1), TimeSpan.FromHours(-1)); lease.Check();
            clock.Advance(TimeSpan.FromSeconds(1), TimeSpan.Zero); Require(lease.Token.IsCancellationRequested); Canceled(lease.Check);
        });
        await CheckAsync("lease-nested-pre-canceled-caller-zero-dependency-start", async () =>
        {
            var clock = new FixtureClock(origin); using var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(10), default);
            using var newerCaller = new CancellationTokenSource(); newerCaller.Cancel(); var starts = 0;
            await CanceledAsync(async () => _ = await lease.InvokeAsync(_ => { starts++; return ValueTask.FromResult(7); }, newerCaller.Token));
            Require(starts == 0);
        });
        foreach (var mode in new[] { "expiry", "caller", "invalidate", "dispose", "UTC-rollback" })
        {
            await CheckAsync("lease-pending-ignored-token-late-result:" + mode, async () =>
            {
                var clock = new FixtureClock(origin); using var caller = new CancellationTokenSource();
                using var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(2), caller.Token);
                var ignored = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = lease.InvokeAsync(_ => new ValueTask<int>(ignored.Task), default).AsTask();
                if (mode == "expiry") clock.Advance(TimeSpan.FromSeconds(2));
                else if (mode == "UTC-rollback") clock.Advance(TimeSpan.FromSeconds(2), TimeSpan.FromHours(-1));
                else if (mode == "caller") caller.Cancel(); else if (mode == "invalidate") lease.Invalidate(); else lease.Dispose();
                await CanceledAsync(async () => _ = await pending.WaitAsync(TimeSpan.FromSeconds(2)));
                ignored.SetResult(7); await ignored.Task; Require(pending.IsCanceled || pending.IsFaulted); Canceled(lease.Check);
            });
        }
        Check("lease-disposal-repeated-refuses-admission", () =>
        {
            var clock = new FixtureClock(origin); var lease = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(2), default);
            var token = lease.Token; lease.Dispose(); lease.Dispose(); Require(token.IsCancellationRequested); Canceled(lease.Check); Canceled(() => lease.Narrow(origin));
        });
        Check("lease-no-zero-or-past-original-window", () =>
        {
            var clock = new FixtureClock(origin); Canceled(() => _ = new OriginalValidityLeaseV1(clock, origin, default));
            Canceled(() => _ = new OriginalValidityLeaseV1(clock, origin - TimeSpan.FromTicks(1), default));
            using var caller = new CancellationTokenSource(); caller.Cancel(); Canceled(() => _ = new OriginalValidityLeaseV1(clock, origin + TimeSpan.FromSeconds(1), caller.Token));
        });
        foreach (var result in results.Where(x => !x.Passed)) Console.WriteLine("FAIL: " + result.Name + " (" + result.FailureType + ")");
        Console.WriteLine($"Portable adapter: {results.Count(x => x.Passed)} PASS / {results.Count(x => !x.Passed)} FAIL; persisted mechanisms NOT EXECUTED.");
        return results;
        void Refusal(string name, byte[] bytes, Func<byte[], byte[]> parser, Func<byte[], byte[]> change)
        {
            Check(name, () => { var malformed = change(bytes); Require(!malformed.SequenceEqual(bytes)); Deny(() => parser(malformed)); });
        }
    }
    private static byte[] Original(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", name));
    private static byte[] DuplicateRoot(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes); var first = document.RootElement.EnumerateObject().First();
        return Encoding.UTF8.GetBytes("{\"" + first.Name + "\":" + first.Value.GetRawText() + "," + Encoding.UTF8.GetString(bytes)[1..]);
    }
    private static byte[] AliasDuplicateRoot(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes); var first = document.RootElement.EnumerateObject().First();
        var alias = "\\u" + ((int)first.Name[0]).ToString("x4", System.Globalization.CultureInfo.InvariantCulture) + first.Name[1..];
        return Encoding.UTF8.GetBytes("{\"" + alias + "\":" + first.Value.GetRawText() + "," + Encoding.UTF8.GetString(bytes)[1..]);
    }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Independent portable adapter assertion failed."); }
    private static void Deny(Action action)
    { try { action(); } catch (PublicationIntegrityException) { return; } throw new InvalidOperationException("Malformed original control was accepted."); }
    private static void Canceled(Action action)
    { try { action(); } catch (OperationCanceledException) { return; } throw new InvalidOperationException("Expired or canceled lease was accepted."); }
    private static async Task CanceledAsync(Func<Task> action)
    { try { await action(); } catch (OperationCanceledException) { return; } throw new InvalidOperationException("Expired or canceled pending result escaped."); }
}

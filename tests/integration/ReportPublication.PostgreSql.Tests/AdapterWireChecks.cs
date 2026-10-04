using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ReportPublication;
using ReportPublication.PostgreSql;

internal static class AdapterWireChecks
{
    internal static IReadOnlyList<AdapterPortableChecks.Result> Run()
    {
        var results = new List<AdapterPortableChecks.Result>();
        void Check(string name, Action test)
        {
            try { test(); results.Add(new(name, true, null)); }
            catch (Exception ex) { results.Add(new(name, false, ex.GetType().Name)); }
        }
        foreach (var bundle in new[] { "completed", "warned", "source-limitation" })
        {
            var original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", bundle + ".command.json"));
            var node = JsonNode.Parse(original)!; var actor = FrozenFixtureLoader.Load<PublicationActorV1>(node["actor"]!);
            var scope = FrozenFixtureLoader.Load<PublicationScopeV1>(node["scope"]!);
            node["invocationId"] = "00000000-0000-4000-8000-000000000911"; node["correlationId"] = "00000000-0000-4000-8000-000000000912";
            var command = FrozenFixtureLoader.Load<PublishCommandV1>(node); var bound = FixtureBoundOperationV1.Publish(actor, command);
            Check(bundle + ":scope-original", () => Require(NativeFixtureWireV1.Scope(scope).SequenceEqual(Encoding.UTF8.GetBytes(node["scope"]!.ToJsonString()))));
            Check(bundle + ":actor-original", () => Require(NativeFixtureWireV1.Actor(actor).SequenceEqual(Encoding.UTF8.GetBytes(node["actor"]!.ToJsonString()))));
            Check(bundle + ":command-original", () => Require(bound.CopyBytes().SequenceEqual(original) && bound.Digest == Convert.ToHexStringLower(SHA256.HashData(original))));
            Check(bundle + ":command-owned-copy", () => { var bytes = bound.CopyBytes(); Array.Clear(bytes); Require(bound.CopyBytes().SequenceEqual(original)); });
            Check(bundle + ":command-canonical-copy", () => Require(bound.Matches(actor, Copy(command))));
            Check(bundle + ":command-excluded-invocation-correlation", () => Require(bound.Matches(actor, Copy(command, invocation: NewId, correlation: NewId))));
            Check(bundle + ":command-revision-deny", () => Require(!bound.Matches(actor, Copy(command, revision: command.ExpectedRunRevision + 1))));
            Check(bundle + ":command-digest-deny", () => Require(!bound.Matches(actor, Copy(command, digest: new string('a', 64)))));
            Check(bundle + ":command-run-deny", () => Require(!bound.Matches(actor, Copy(command, run: NewId))));
            Check(bundle + ":command-operation-deny", () => Require(!bound.Matches(actor, Copy(command, operation: NewId))));
            Check(bundle + ":command-actor-deny", () => Require(!bound.Matches(actor with { SecurityVersion = actor.SecurityVersion + 1 }, command)));
            Check(bundle + ":command-scope-deny", () => Require(!bound.Matches(actor, Copy(command, scope: scope with { CustomerId = NewId }))));
            var admitted = new FixtureInvocationAdmissionV1(actor, scope, FixtureInvocationPurposeV1.Publish, command.RunId, command.OperationId,
                new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero));
            Check(bundle + ":admission-exact", () => Require(bound.Matches(admitted)));
            Check(bundle + ":admission-null-deny", () => Require(!bound.Matches((FixtureInvocationAdmissionV1)null!)));
            Check(bundle + ":admission-purpose-deny", () => Require(!bound.Matches(admitted with { Purpose = FixtureInvocationPurposeV1.ReadExact })));
            Check(bundle + ":admission-resource-deny", () => Require(!bound.Matches(admitted with { ResourceId = NewId })));
            Check(bundle + ":admission-operation-deny", () => Require(!bound.Matches(admitted with { OriginalOperationOrInvocationId = NewId })));
            Check(bundle + ":admission-actor-deny", () => Require(!bound.Matches(admitted with { Actor = actor with { SessionId = NewId } })));
            Check(bundle + ":admission-scope-deny", () => Require(!bound.Matches(admitted with { Scope = scope with { EnvironmentId = NewId } })));
            Check(bundle + ":admission-offset-deny", () => Require(!bound.Matches(admitted with { OriginalAuthorityDeadlineUtc = admitted.OriginalAuthorityDeadlineUtc.ToOffset(TimeSpan.FromHours(1)) })));
            var readBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", bundle + ".read-request.json"));
            var readNode = JsonNode.Parse(readBytes)!; readNode["correlationId"] = "00000000-0000-4000-8000-000000000912";
            var request = FrozenFixtureLoader.Load<ExactReportRequestV1>(readNode); var read = FixtureBoundOperationV1.Read(actor, request);
            Check(bundle + ":read-original", () => Require(read.CopyBytes().SequenceEqual(readBytes) && read.Digest == Convert.ToHexStringLower(SHA256.HashData(readBytes))));
            Check(bundle + ":read-copy-owned", () => { var b = read.CopyBytes(); Array.Clear(b); Require(read.CopyBytes().SequenceEqual(readBytes)); });
            Check(bundle + ":read-correlation-excluded", () => Require(read.Matches(actor, request with { CorrelationId = NewId })));
            Check(bundle + ":read-invocation-deny", () => Require(!read.Matches(actor, request with { InvocationId = NewId })));
            Check(bundle + ":read-resource-deny", () => Require(!read.Matches(actor, request with { ReportVersionId = NewId })));
            Check(bundle + ":read-digest-deny", () => Require(!read.Matches(actor, request with { ExpectedManifestDigest = new string('a', 64) })));
            Check(bundle + ":read-publish-purpose-deny", () => Require(!read.Matches(actor, command) && !bound.Matches(actor, request)));
        }
        foreach (var bad in new[] { "", new string('a', 129), "a/b", "a\0b", "é", "a b" })
            Check("tokens-deny:" + Convert.ToHexString(Encoding.UTF8.GetBytes(bad)), () => Deny(() => NativeFixtureWireV1.Tokens(new[] { bad })));
        Check("tokens-null-set", () => Deny(() => NativeFixtureWireV1.Tokens(null!)));
        Check("tokens-null-item", () => Deny(() => NativeFixtureWireV1.Tokens(new[] { (string)null! })));
        Check("tokens-duplicate", () => Deny(() => NativeFixtureWireV1.Tokens(new[] { "A", "A" })));
        Check("tokens-empty", () => Require(Encoding.UTF8.GetString(NativeFixtureWireV1.Tokens(Array.Empty<string>())) == "[]"));
        Check("tokens-ordinal-copy", () => { var supplied = new[] { "a", "A", "a.b_9-x" }; var b = NativeFixtureWireV1.Tokens(supplied); supplied[0] = "changed"; Require(Encoding.UTF8.GetString(b) == "[\"A\",\"a\",\"a.b_9-x\"]"); });
        Check("tokens-length-boundary", () => Require(Encoding.UTF8.GetString(NativeFixtureWireV1.Tokens(new[] { new string('a', 128) })).Length == 132));
        var binding = new PostgreSqlPublicationBindingV1(NewId, Guid.Parse("00000000-0000-4000-8000-000000000921"), Guid.Parse("00000000-0000-4000-8000-000000000922"));
        Check("binding-exact", () => NativeFixtureWireV1.Binding(binding));
        Check("binding-null", () => Deny(() => NativeFixtureWireV1.Binding(null!)));
        Check("binding-zero-customer", () => Deny(() => NativeFixtureWireV1.Binding(binding with { CustomerId = Guid.Empty })));
        Check("binding-zero-stream", () => Deny(() => NativeFixtureWireV1.Binding(binding with { StreamId = Guid.Empty })));
        Check("binding-zero-writer", () => Deny(() => NativeFixtureWireV1.Binding(binding with { WriterBindingReference = Guid.Empty })));
        const string utc = "2026-10-04T01:00:00.0000001Z"; var ticks = new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero).UtcTicks + 1;
        Check("clock-exact", () => Require(NativeFixtureWireV1.Clock(utc, ticks).UtcTicks == ticks));
        Check("clock-ticks-mismatch", () => Deny(() => NativeFixtureWireV1.Clock(utc, ticks + 1)));
        foreach (var bad in new[] { "2026-10-04T01:00:00Z", "2026-10-04T01:00:00.0000001+00:00", "2026-02-30T01:00:00.0000001Z", "2026-10-04T01:00:00.0000001z" })
            Check("clock-format-deny:" + bad, () => Deny(() => NativeFixtureWireV1.Clock(bad, ticks)));
        foreach (var bytes in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("abc"), new byte[] { 0, 1, 255 }, Encoding.UTF8.GetBytes("雪\0é") })
            Check("digest-literal:" + Convert.ToHexString(bytes), () => Require(NativeFixtureWireV1.Digest(bytes) == Convert.ToHexStringLower(SHA256.HashData(bytes))));
        Check("actor-null", () => Deny(() => NativeFixtureWireV1.Actor(null!)));
        Check("scope-null", () => Deny(() => NativeFixtureWireV1.Scope(null!)));
        Check("publish-null-arguments", () => Deny(() => FixtureBoundOperationV1.Publish(null!, null!)));
        Check("read-null-arguments", () => Deny(() => FixtureBoundOperationV1.Read(null!, null!)));
        foreach (var r in results.Where(x => !x.Passed)) Console.WriteLine("FAIL: " + r.Name + " (" + r.FailureType + ")");
        Console.WriteLine($"Portable wire: {results.Count(x => x.Passed)} PASS / {results.Count(x => !x.Passed)} FAIL; persisted mechanisms NOT EXECUTED.");
        return results;
    }
    private static readonly Guid NewId = Guid.Parse("00000000-0000-4000-8000-000000000920");
    private static PublishCommandV1 Copy(PublishCommandV1 c, Guid? invocation = null, Guid? correlation = null, long? revision = null,
        string? digest = null, Guid? run = null, Guid? operation = null, PublicationScopeV1? scope = null) => new(operation ?? c.OperationId,
            invocation ?? c.InvocationId, correlation ?? c.CorrelationId, scope ?? c.Scope, run ?? c.RunId, revision ?? c.ExpectedRunRevision,
            digest ?? c.ExpectedSourceDigest, c.AcknowledgedWarnings);
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Independent wire assertion failed."); }
    private static void Deny(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (PublicationIntegrityException) { return; }
        throw new InvalidOperationException("Invalid fixture wire accepted.");
    }
}

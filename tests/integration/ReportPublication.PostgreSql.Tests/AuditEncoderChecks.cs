using System.Text.Json.Nodes;
using ReportPublication;
using ReportPublication.PostgreSql;

internal static class AuditEncoderChecks
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
            foreach (var action in new[] { "publish-event", "read-event" })
            {
                var name = bundle + ":" + action;
                var original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", bundle + "." + action + ".json"));
                var e = FrozenFixtureLoader.Load<PublicationAuditEventV1>(JsonNode.Parse(original)!);
                var binding = new PostgreSqlPublicationBindingV1(e.Scope!.CustomerId, e.StreamId, e.WriterBindingReference);
                var head = new FixtureAuditHeadV1(e.EventId, e.Sequence, e.PreviousEventDigest, e.EventAtUtc);
                var intent = new PublicationAuditIntentV1(e.OperationId, e.InvocationId, e.CorrelationId, e.Actor, e.Scope,
                    e.ResourceKind, e.ResourceId, e.Action, e.Outcome, e.Reason, e.ManifestDigest, e.ReturnedFields);
                Check(name + ":original-byteexact", () => Require(FixtureAuditEncodingV1.Encode(binding, head, intent).SequenceEqual(original)));
                Check(name + ":returned-byteownership", () => { var b = FixtureAuditEncodingV1.Encode(binding, head, intent); Array.Clear(b); Require(FixtureAuditEncodingV1.Encode(binding, head, intent).SequenceEqual(original)); });
                Check(name + ":crosscustomer-deny", () => Deny(() => FixtureAuditEncodingV1.Encode(binding with { CustomerId = Other }, head, intent)));
                Check(name + ":head-null", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, null!, intent)));
                Check(name + ":intent-null", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, null!)));
                Check(name + ":binding-null", () => Deny(() => FixtureAuditEncodingV1.Encode(null!, head, intent)));
                Check(name + ":binding-zero", () => Deny(() => FixtureAuditEncodingV1.Encode(binding with { StreamId = Guid.Empty }, head, intent)));
                Check(name + ":head-id-zero", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head with { EventId = Guid.Empty }, intent)));
                Check(name + ":sequence-zero", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head with { Sequence = 0 }, intent)));
                Check(name + ":head-offset", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head with { At = head.At.ToOffset(TimeSpan.FromHours(1)) }, intent)));
                Check(name + ":head-genesis-wrong", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head with { PreviousDigest = e.Sequence == 1 ? new string('a', 64) : new string('0', 64) }, intent)));
                Check(name + ":head-uppercase-digest", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head with { PreviousDigest = new string('A', 64) }, intent)));
                Check(name + ":intent-invalidoutcome", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, Copy(intent, outcome: (PublicationAuditOutcomeV1)99))));
                Check(name + ":intent-wrongreason", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, Copy(intent, reason: PublicationAuditReasonV1.AuthorityDenied))));
                Check(name + ":intent-zeroinvocation", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, Copy(intent, invocation: Guid.Empty))));
                Check(name + ":intent-nullmanifest", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, Copy(intent, omitManifest: true))));
                Check(name + ":intent-wrongresourcekind", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, Copy(intent, resource: e.ResourceKind == PublicationResourceKindV1.Run ? PublicationResourceKindV1.ReportVersion : PublicationResourceKindV1.Run))));
                Check(name + ":intent-fields-copy", () => { var supplied = e.ReturnedFields.ToArray(); var copied = Copy(intent, fields: supplied); Array.Fill(supplied, "changed"); Require(FixtureAuditEncodingV1.Encode(binding, head, copied).SequenceEqual(original)); });
                Check(name + ":intent-returnedfields-invalid", () => Deny(() => FixtureAuditEncodingV1.Encode(binding, head, Copy(intent, fields: new[] { "undeclared" }))));
            }
        Check("anonymous-null-positive", () =>
        {
            var b = new PostgreSqlPublicationBindingV1(Other, Guid.Parse("00000000-0000-4000-8000-000000000401"), Guid.Parse("00000000-0000-4000-8000-000000000501"));
            var head = new FixtureAuditHeadV1(Guid.Parse("00000000-0000-4000-8000-000000000301"), 1, new string('0', 64), new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero));
            var intent = new PublicationAuditIntentV1(null, Guid.Parse("00000000-0000-4000-8000-000000000601"), Guid.Parse("00000000-0000-4000-8000-000000000701"), null, null,
                null, null, PublicationAuditActionV1.ReadExact, PublicationAuditOutcomeV1.Denied, PublicationAuditReasonV1.AuthorityDenied, null, []);
            var bytes = FixtureAuditEncodingV1.Encode(b, head, intent); var node = JsonNode.Parse(bytes)!;
            Require(node["actor"] is null && node["scope"] is null && node["resourceId"] is null && node["actorKind"]!.GetValue<string>() == "Anonymous");
            Require(NativePublicationCanonicalV1.AuditEventBytes(FrozenFixtureLoader.Load<PublicationAuditEventV1>(node)).SequenceEqual(bytes));
        });
        foreach (var r in results.Where(x => !x.Passed)) Console.WriteLine("FAIL: " + r.Name + " (" + r.FailureType + ")");
        Console.WriteLine($"Portable audit encoder: {results.Count(x => x.Passed)} PASS / {results.Count(x => !x.Passed)} FAIL; no SQL/reservation evidence.");
        return results;
    }
    private static readonly Guid Other = Guid.Parse("00000000-0000-4000-8000-000000000991");
    private static PublicationAuditIntentV1 Copy(PublicationAuditIntentV1 i, PublicationAuditOutcomeV1? outcome = null, PublicationAuditReasonV1? reason = null,
        Guid? invocation = null, bool omitManifest = false, PublicationResourceKindV1? resource = null, IEnumerable<string>? fields = null) => new(i.OperationId,
            invocation ?? i.InvocationId, i.CorrelationId, i.VerifiedActor, i.VerifiedScope, resource ?? i.ResourceKind, i.VerifiedResourceId, i.Action,
            outcome ?? i.Outcome, reason ?? i.Reason, omitManifest ? null : i.ManifestDigest, fields ?? i.ReturnedFields);
    private static void Require(bool v) { if (!v) throw new InvalidOperationException("Independent audit encoder assertion failed."); }
    private static void Deny(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid bound audit intent accepted.");
    }
}

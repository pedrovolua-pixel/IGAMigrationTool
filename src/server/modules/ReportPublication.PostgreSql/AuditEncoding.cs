using ReportPublication;

namespace ReportPublication.PostgreSql;

internal sealed record FixtureAuditHeadV1(Guid EventId, long Sequence, string PreviousDigest, DateTimeOffset At);

internal static class FixtureAuditEncodingV1
{
    internal static byte[] Encode(PostgreSqlPublicationBindingV1 binding, FixtureAuditHeadV1 head, PublicationAuditIntentV1 intent)
    {
        NativeFixtureWireV1.Binding(binding);
        if (head is null || head.EventId == Guid.Empty || head.Sequence < 1 || intent is null) throw NativeFixtureWireV1.Invalid();
        return NativePublicationCanonicalV1.AuditEventBytes(new(head.EventId, binding.StreamId, binding.WriterBindingReference,
            head.Sequence, head.PreviousDigest, head.At, intent.OperationId, intent.InvocationId, intent.CorrelationId,
            intent.VerifiedActor is null ? PublicationAuditActorKindV1.Anonymous : PublicationAuditActorKindV1.Human,
            intent.VerifiedActor, intent.VerifiedScope, intent.ResourceKind, intent.VerifiedResourceId,
            intent.Action, intent.Outcome, intent.Reason, intent.ManifestDigest, intent.ReturnedFields, []));
    }
}

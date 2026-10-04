namespace ReportPublication;

public static partial class NativePublicationCanonicalV1
{
    public static byte[] ManifestBytes(PublicationManifestV1 m)
    {
        Require(PublicationValidationV1.Manifest(m));
        return Encode(Map(("schemaVersion", "health-report-manifest-v1"), ("projectionSchemaVersion", "health-report-projection-v1"),
            ("scoreSchemaVersion", "health-report-score-v1"), ("scope", Scope(m.Scope, false)), ("assessmentId", Id(m.Scope.AssessmentId)),
            ("reportVersionId", Id(m.ReportVersionId)), ("runId", Id(m.RunId)), ("runRevision", Counter(m.RunRevision)),
            ("createdAtUtc", Time(m.CreatedAtUtc)), ("createdBy", Map(("tenantId", Id(m.CreatedBy.TenantId)), ("objectId", Id(m.CreatedBy.ObjectId)))),
            ("assessmentState", m.AssessmentState.ToString()), ("approvalState", m.ApprovalState.ToString()), ("inputs", Inputs(m.Inputs)),
            ("projectionDigest", m.ProjectionDigest), ("scoreDigest", m.ScoreDigest), ("sourceDigest", m.SourceDigest),
            ("requiredCategories", Tokens(m.RequiredCategories)), ("requiredFields", Tokens(m.RequiredFields)),
            ("classification", "MinimizedDerivedReport"), ("redactionMarkers", m.RedactionMarkers.OrderBy(v => v.Section, StringComparer.Ordinal)
                .ThenBy(v => Id(v.Id), StringComparer.Ordinal).ThenBy(v => v.Field, StringComparer.Ordinal).ThenBy(v => v.Reason.ToString(), StringComparer.Ordinal)
                .Select(v => (object?)Map(("section", v.Section), ("id", Id(v.Id)), ("field", v.Field), ("reason", v.Reason.ToString()))).ToArray()),
            ("retention", Retention(m.Retention)), ("provenance", m.Provenance.OrderBy(v => v.Kind, StringComparer.Ordinal)
                .ThenBy(v => Id(v.OpaqueRecordId), StringComparer.Ordinal).Select(v => (object?)Map(("kind", v.Kind),
                    ("opaqueRecordId", Id(v.OpaqueRecordId)), ("digest", v.Digest))).ToArray()),
            ("artifactInputs", m.ArtifactInputs.OrderBy(v => v.Kind.ToString(), StringComparer.Ordinal)
                .Select(v => (object?)Map(("kind", v.Kind.ToString()), ("digest", v.Digest), ("byteLength", Counter(v.ByteLength)))).ToArray())), 1024 * 1024);
    }

    public static byte[] AuditEventBytes(PublicationAuditEventV1 e)
    {
        Require(PublicationValidationV1.AuditEvent(e));
        return Encode(Map(("schemaVersion", "report-publication-audit-v1"), ("eventId", Id(e.EventId)), ("streamId", Id(e.StreamId)),
            ("writerBindingReference", Id(e.WriterBindingReference)), ("sequence", Counter(e.Sequence)),
            ("previousEventDigest", e.PreviousEventDigest), ("eventAtUtc", Time(e.EventAtUtc)),
            ("operationId", e.OperationId is { } op ? Id(op) : null), ("invocationId", Id(e.InvocationId)), ("correlationId", Id(e.CorrelationId)),
            ("actorKind", e.ActorKind.ToString()), ("actor", e.Actor is { } actor ? Actor(actor) : null),
            ("scope", e.Scope is { } scope ? Scope(scope, true) : null), ("resourceKind", e.ResourceKind?.ToString()),
            ("resourceId", e.ResourceId is { } resource ? Id(resource) : null), ("action", e.Action.ToString()),
            ("outcome", e.Outcome.ToString()), ("reason", e.Reason.ToString()), ("manifestDigest", e.ManifestDigest),
            ("returnedFields", Tokens(e.ReturnedFields)), ("redactedFields", Tokens(e.RedactedFields))), MaximumCommandBytes);
    }

    public static byte[] PublicationReceiptBytes(PublicationReceiptV1 r)
    {
        Require(PublicationValidationV1.PublicationReceipt(r));
        return Encode(Map(("schemaVersion", "report-publication-receipt-v1"), ("operationId", Id(r.OperationId)), ("actor", Actor(r.Actor)),
            ("scope", Scope(r.Scope, true)), ("runId", Id(r.RunId)), ("expectedRunRevision", Counter(r.ExpectedRunRevision)),
            ("commandDigest", r.CommandDigest), ("reportVersionId", Id(r.ReportVersionId)), ("manifestDigest", r.ManifestDigest),
            ("projectionDigest", r.ProjectionDigest), ("scoreDigest", r.ScoreDigest), ("committedAtUtc", Time(r.CommittedAtUtc)),
            ("eventId", Id(r.EventId)), ("eventDigest", r.EventDigest)), MaximumCommandBytes);
    }

    public static byte[] ReadReceiptBytes(ExactReadReceiptV1 r)
    {
        Require(PublicationValidationV1.ReadReceipt(r));
        return Encode(Map(("schemaVersion", "report-exact-read-receipt-v1"), ("invocationId", Id(r.InvocationId)), ("requestDigest", r.RequestDigest),
            ("actor", Actor(r.Actor)), ("scope", Scope(r.Scope, true)), ("reportVersionId", Id(r.ReportVersionId)), ("manifestDigest", r.ManifestDigest),
            ("committedAtUtc", Time(r.CommittedAtUtc)), ("eventId", Id(r.EventId)), ("eventDigest", r.EventDigest)), MaximumCommandBytes);
    }
}

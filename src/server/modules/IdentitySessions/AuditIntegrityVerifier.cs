using System.Text.Json;

namespace IdentitySessions;

public sealed record AuditIntegrityEntryV1(Guid EventId, Guid StreamId, long Sequence, string PreviousSha256,
    string EventSha256, string? CanonicalEvent, Guid? LifecycleAuthority);

public static class AuditIntegrityVerifier
{
    // A checkpoint is an independently supplied witness, never an automatically
    // trusted replacement made from the database being restored/checked.
    public static void Verify(SecurityAuditBindingV1 binding, AuditCheckpointV1 witness, long headSequence,
        string headDigest, IReadOnlyList<AuditIntegrityEntryV1> entries, IReadOnlyList<OperationReceiptV1> receipts)
    {
        SecurityAuditCanonical.Validate(binding);
        if (witness.StreamId != binding.StreamId || witness.EnvironmentId != binding.EnvironmentId ||
            witness.WriterBindingReference != binding.WriterBindingReference || witness.Sequence < 0 ||
            witness.WitnessReference == Guid.Empty || witness.CapturedAtUtc.Offset != TimeSpan.Zero ||
            !SecurityAuditCanonical.Digest(witness.EventSha256) || headSequence < witness.Sequence)
            throw new InvalidOperationException("Invalid or regressed independent audit checkpoint.");
        var sequence = 0L; var digest = SecurityAuditCanonical.EmptyDigest;
        var identities = new HashSet<Guid>(); var mutationEvents = new HashSet<Guid>();
        var decoded = new Dictionary<Guid, SecurityAuditEventV1>();
        var lastEventAt = DateTimeOffset.MinValue;
        if (witness.Sequence == 0 && witness.EventSha256 != digest) throw new InvalidOperationException("Invalid empty witness.");
        foreach (var entry in entries.OrderBy(e => e.Sequence))
        {
            if (entry.StreamId != binding.StreamId || !identities.Add(entry.EventId) || entry.EventId == Guid.Empty ||
                entry.Sequence != checked(sequence + 1) || entry.PreviousSha256 != digest || !SecurityAuditCanonical.Digest(entry.EventSha256))
                throw new InvalidOperationException("Audit gap, ordering or identity mismatch.");
            if (entry.CanonicalEvent is { } canonical)
            {
                if (SecurityAuditCanonical.Hash(canonical) != entry.EventSha256 || entry.LifecycleAuthority is not null)
                    throw new InvalidOperationException("Audit event digest mismatch.");
                using var document = JsonDocument.Parse(canonical); var value = document.RootElement;
                var evt = SecurityAuditCanonical.ReadEvent(binding, canonical, entry.Sequence, entry.PreviousSha256);
                if (evt.EventId != entry.EventId) throw new InvalidOperationException("Audit entry identity conflict.");
                if (evt.EventAtUtc < lastEventAt || entry.Sequence <= witness.Sequence && evt.EventAtUtc > witness.CapturedAtUtc)
                    throw new InvalidOperationException("Audit time anomaly against independent witness.");
                lastEventAt = evt.EventAtUtc;
                decoded.Add(evt.EventId, evt);
                if (value.GetProperty("writerBindingReference").GetString() != binding.WriterBindingReference.ToString("D") ||
                    value.GetProperty("environmentId").GetString() != binding.EnvironmentId ||
                    value.GetProperty("eventId").GetString() != entry.EventId.ToString("D") ||
                    value.GetProperty("sequence").GetString() != entry.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Unknown audit writer/event binding.");
                if (value.GetProperty("action").GetString() is "SessionIssued" or "SessionRevoked" or "SessionRotated" or "SubjectRevoked" or "AuthorityChanged")
                    mutationEvents.Add(entry.EventId);
            }
            else if (entry.LifecycleAuthority is null || entry.LifecycleAuthority == Guid.Empty)
                throw new InvalidOperationException("Unattributed audit deletion gap.");
            sequence = entry.Sequence; digest = entry.EventSha256;
            if (sequence == witness.Sequence && digest != witness.EventSha256) throw new InvalidOperationException("Independent audit witness mismatch.");
        }
        if (sequence != headSequence || digest != headDigest) throw new InvalidOperationException("Audit head mismatch.");
        var receiptEvents = new HashSet<Guid>();
        foreach (var receipt in receipts)
        {
            _ = SecurityAuditCanonical.Receipt(receipt);
            foreach (var eventId in receipt.EventIds)
            {
                if (!identities.Contains(eventId) || !receiptEvents.Add(eventId)) throw new InvalidOperationException("Receipt refers to missing/duplicated event.");
                if (decoded.TryGetValue(eventId, out var evt) && (evt.OperationId != receipt.Request.OperationId ||
                    evt.ActorKind != receipt.Request.ActorKind || evt.Actor != receipt.Request.Actor || evt.Target != receipt.Request.Target ||
                    evt.Action != receipt.Request.OperationKind || evt.Outcome != receipt.Outcome || evt.SecurityVersion != receipt.SecurityVersion ||
                    evt.EventAtUtc > receipt.CommittedAtUtc || evt.CustomerId != receipt.Request.CustomerId || evt.ProjectId != receipt.Request.ProjectId ||
                    receipt.Request.Issuer != binding.Writer ||
                    evt.Action is SecurityAuditAction.SessionIssued or SecurityAuditAction.SessionRotated && evt.SessionReference != receipt.NewSessionReference ||
                    evt.Action == SecurityAuditAction.SessionRevoked && evt.SessionReference != receipt.Request.OldSessionReference ||
                    evt.Action == SecurityAuditAction.SessionRotated && evt.PreviousSessionReference != receipt.Request.OldSessionReference))
                    throw new InvalidOperationException("Receipt/event authority binding mismatch.");
            }
        }
        if (!mutationEvents.IsSubsetOf(receiptEvents)) throw new InvalidOperationException("Mutation event lacks durable receipt.");
    }
}

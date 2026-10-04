using System.Security.Cryptography;

namespace SyntheticMcp;

internal sealed class CursorRegistry
{
    internal sealed record Entry(ReadGrant Grant, int PageSize, int Position, TimeSpan Expires);
    internal sealed record Reservation(string Handle, Entry Entry);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> reservations = new(StringComparer.Ordinal);

    internal void Purge(ReadGrant grant, TimeSpan now)
    {
        foreach (var pair in entries.ToArray())
            if (now >= pair.Value.Expires || SameIdentity(pair.Value.Grant, grant) && pair.Value.Grant.Scope == grant.Scope && pair.Value.Grant.Revision != grant.Revision)
                entries.Remove(pair.Key);
    }
    internal Entry? Resolve(string handle, ReadGrant grant, int pageSize, TimeSpan now)
    {
        Purge(grant, now);
        if (handle.Length != 64 || handle.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            !entries.TryGetValue(handle, out var entry) || entry.PageSize != pageSize || !Bound(entry.Grant, grant)) return null;
        return entry;
    }
    internal Reservation? Reserve(ReadGrant grant, int pageSize, int position, TimeSpan expires, TimeSpan now)
    {
        Purge(grant, now);
        var all = entries.Values.Concat(reservations.Values).ToArray();
        if (expires <= now || all.Length >= 4096 || all.Count(e => SameIdentity(e.Grant, grant)) >= 128 ||
            all.Count(e => e.Grant.Scope.CustomerId == grant.Scope.CustomerId) >= 512) return null;
        string handle;
        do { handle = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)); }
        while (entries.ContainsKey(handle) || reservations.ContainsKey(handle));
        var entry = new Entry(grant, pageSize, position, expires);
        reservations.Add(handle, entry);
        return new Reservation(handle, entry);
    }
    internal void Publish(Reservation reservation)
    {
        reservations.Remove(reservation.Handle);
        entries.Add(reservation.Handle, reservation.Entry);
    }
    internal void Release(Reservation? reservation)
    {
        if (reservation is not null) reservations.Remove(reservation.Handle);
    }
    internal void Remove(string? handle)
    {
        if (handle is not null) entries.Remove(handle);
    }
    private static bool SameIdentity(ReadGrant a, ReadGrant b) => a.IdentityKind == b.IdentityKind && a.IdentityId == b.IdentityId;
    private static bool Bound(ReadGrant a, ReadGrant b) => SameIdentity(a, b) && a.Revision == b.Revision && a.Scope == b.Scope &&
        a.Kind == b.Kind && a.AssessmentId == b.AssessmentId && a.ReportVersionId == b.ReportVersionId && a.ManifestDigest == b.ManifestDigest;
}

namespace SyntheticMcp;

// All methods run under the harness's single local bookkeeping fence.
internal sealed class LocalLimits
{
    private sealed class Bucket
    {
        internal readonly Queue<TimeSpan> Times = new();
        internal int Active;
    }

    private readonly Dictionary<(IdentityKind, string), Bucket> identities = new();
    private readonly Dictionary<string, Bucket> customers = new(StringComparer.Ordinal);
    private readonly Bucket ingress = new();
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    internal Action? Acquire(Caller caller, ReadGrant? grant, TimeSpan now)
    {
        Prune(identities, now);
        Prune(customers, now);
        Trim(ingress, now);
        if (grant is null)
        {
            if (ingress.Active >= 16 || ingress.Times.Count >= 600) return null;
            ingress.Active++;
            ingress.Times.Enqueue(now);
            return () => ingress.Active--;
        }
        var identityKey = (caller.Kind, caller.IdentityId);
        var customerKey = grant.Scope.CustomerId;
        identities.TryGetValue(identityKey, out var identity);
        customers.TryGetValue(customerKey, out var customer);
        if (identity is null && identities.Count >= 4096 || customer is null && customers.Count >= 4096 ||
            identity is { Active: >= 4 } || customer is { Active: >= 16 } ||
            identity?.Times.Count >= 60 || customer?.Times.Count >= 300) return null;
        identity ??= new Bucket();
        customer ??= new Bucket();
        identities[identityKey] = identity;
        customers[customerKey] = customer;
        identity.Active++;
        customer.Active++;
        identity.Times.Enqueue(now);
        customer.Times.Enqueue(now);
        return () => { identity.Active--; customer.Active--; };
    }

    private static void Trim(Bucket bucket, TimeSpan now)
    {
        while (bucket.Times.TryPeek(out var first) && now - first >= Window) bucket.Times.Dequeue();
    }
    private static void Prune<TKey>(Dictionary<TKey, Bucket> buckets, TimeSpan now) where TKey : notnull
    {
        foreach (var entry in buckets.ToArray())
        {
            Trim(entry.Value, now);
            if (entry.Value.Active == 0 && entry.Value.Times.Count == 0) buckets.Remove(entry.Key);
        }
    }
}

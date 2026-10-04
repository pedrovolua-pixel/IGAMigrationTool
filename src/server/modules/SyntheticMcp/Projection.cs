using System.Collections.Immutable;
using System.Text.Json;

namespace SyntheticMcp;

/// <summary>Closed, category-filtered fictional resources with unchanged publication provenance.</summary>
public static class Projection
{
    private static readonly ImmutableDictionary<ResourceKind, ImmutableHashSet<string>> Vocabularies = new Dictionary<ResourceKind, string[]>
    {
        [ResourceKind.PublishedStatus] = ["itemId", "category", "assessmentState", "approvalState", "limitations"],
        [ResourceKind.Coverage] = ["itemId", "category", "assessed", "gap", "unavailable", "label", "limitations"],
        [ResourceKind.Scores] = ["itemId", "category", "health", "quality", "maturity", "reason"],
        [ResourceKind.Findings] = ["itemId", "category", "title", "summary", "severity", "reviewState", "confidence", "mandatoryReview", "referenceIds"],
        [ResourceKind.Recommendations] = ["itemId", "category", "findingId", "summary", "options", "priority", "effort", "reviewLabel"],
        [ResourceKind.ProtectedReferences] = ["itemId", "category", "availability", "reason", "currentAvailability", "availabilityReason"]
    }.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutableHashSet(StringComparer.Ordinal));

    public static ImmutableHashSet<string> Fields(ResourceKind kind) => Vocabularies.GetValueOrDefault(kind, ImmutableHashSet<string>.Empty);
    internal static IEnumerable<string> SourceFields(ResourceKind kind) => Fields(kind).Except(["currentAvailability", "availabilityReason"]);

    public static ProjectedItem? Project(JsonElement payload, ItemDescriptor descriptor, ValidatedManifest manifest, ReadGrant grant, ReferenceOverlay? overlay)
    {
        try
        {
            if (descriptor is null || manifest is null || grant is null || grant.Scope != manifest.Bindings.Scope || grant.AssessmentId != manifest.Bindings.AssessmentId || grant.ReportVersionId != manifest.Bindings.ReportVersionId || grant.ManifestDigest != manifest.Digest || grant.Kind != descriptor.Kind || grant.Categories is null || grant.Fields is null || !grant.Categories.Contains(descriptor.Category) || grant.Categories.Any(x => !Enum.IsDefined(x)) || !grant.Fields.IsSubsetOf(Fields(descriptor.Kind))) return null;
            if (PublicationCodec.ReadPayload(PublicationCodec.CanonicalBytes(payload).ToImmutableArray(), descriptor, manifest) is null) return null;
            if (descriptor.Kind == ResourceKind.ProtectedReferences && (overlay is null || !Enum.IsDefined(overlay.CurrentAvailability) || !Enum.IsDefined(overlay.AvailabilityReason))) return null;
            var visible = new Dictionary<string, object?>(StringComparer.Ordinal);
            var redacted = ImmutableArray.CreateBuilder<string>();
            foreach (var field in Fields(descriptor.Kind).Order(StringComparer.Ordinal))
            {
                if (field is not ("itemId" or "category") && !grant.Fields.Contains(field)) { redacted.Add(field); continue; }
                if (field == "currentAvailability") visible[field] = overlay!.CurrentAvailability.ToString();
                else if (field == "availabilityReason") visible[field] = overlay!.AvailabilityReason.ToString();
                else if (field == "referenceIds")
                {
                    var permitted = payload.GetProperty(field).EnumerateArray().Select(x => x.GetString()!).Where(id => manifest.Items.Any(x => x.Kind == ResourceKind.ProtectedReferences && x.ItemId == id && grant.Categories.Contains(x.Category))).ToArray();
                    visible[field] = permitted;
                    if (permitted.Length != payload.GetProperty(field).GetArrayLength()) redacted.Add(field);
                }
                else if (field == "findingId" && !manifest.Items.Any(x => x.Kind == ResourceKind.Findings && x.ItemId == payload.GetProperty(field).GetString() && grant.Categories.Contains(x.Category))) redacted.Add(field);
                else visible[field] = payload.GetProperty(field).Clone();
            }
            return new(JsonSerializer.SerializeToElement(visible).Clone(), visible.Keys.Order(StringComparer.Ordinal).ToImmutableArray(), redacted.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());
        }
        catch (Exception exception) when (PublicationCodec.Malformed(exception)) { return null; }
    }

    public static ImmutableArray<byte> Envelope(ValidatedManifest manifest, ResourceKind kind, IEnumerable<ProjectedItem> items, string? nextCursor)
    {
        try
        {
            if (manifest is null || !Enum.IsDefined(kind) || items is null || (nextCursor is not null && !PublicationCodec.IsDigest(nextCursor))) return [];
            var bindings = manifest.Bindings;
            var frozen = new Dictionary<string, object?>
            {
                ["contractVersion"] = bindings.ContractVersion,
                ["fixtureKind"] = bindings.FixtureKind,
                ["customerId"] = bindings.Scope.CustomerId,
                ["projectId"] = bindings.Scope.ProjectId,
                ["environmentId"] = bindings.Scope.EnvironmentId,
                ["assessmentId"] = bindings.AssessmentId,
                ["reportVersionId"] = bindings.ReportVersionId,
                ["baselineVersion"] = bindings.BaselineVersion,
                ["catalogVersion"] = bindings.CatalogVersion,
                ["scoringProfileVersion"] = bindings.ScoringProfileVersion,
                ["maturityProfileVersion"] = bindings.MaturityProfileVersion,
                ["applicationVersion"] = bindings.ApplicationVersion,
                ["assessmentState"] = bindings.AssessmentState,
                ["approvalState"] = bindings.ApprovalState
            };
            var envelope = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["contractVersion"] = McpContract.Version,
                ["resourceKind"] = kind.ToString(),
                ["manifestDigest"] = manifest.Digest,
                ["bindings"] = frozen,
                ["items"] = items.Select(x => x.Content).ToArray(),
                ["nextCursor"] = nextCursor
            });
            return PublicationCodec.CanonicalBytes(envelope).ToImmutableArray();
        }
        catch (Exception exception) when (PublicationCodec.Malformed(exception)) { return []; }
    }
}

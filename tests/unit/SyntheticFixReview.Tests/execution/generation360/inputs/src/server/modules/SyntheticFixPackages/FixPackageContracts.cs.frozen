using System.Collections.Immutable;
using RecommendationGuidance;

namespace SyntheticFixPackages;

public enum FixPackageIssue { MissingGuidance, InvalidGuidance, IntegrityMismatch, OutputTooLarge }
public sealed record FixTemplate(string TemplateId, string Kind, string Text);
public sealed record FixArtifact(string ArtifactId, string TemplateId, string Kind, string Status, string Text);
public sealed record FixOption(string ScopedOptionId, ImmutableArray<FixArtifact> Artifacts);
public sealed record FixPackage(string PackageId, string FindingId, ImmutableArray<FixOption> Options);

/// <summary>Detached fictional examples only; no authority, review, persistence or execution.</summary>
public sealed class FixPackageSnapshot
{
    internal FixPackageSnapshot(string schemaVersion, string status, string disclaimer, GuidanceSnapshot guidance,
        string templateVersion, string templateDigest, ImmutableArray<FixTemplate> templates,
        ImmutableArray<FixPackage> packages, ImmutableArray<string> warnings, ImmutableArray<string> unavailableSections,
        string canonicalJson, string contentDigest)
    {
        SchemaVersion = schemaVersion;
        Status = status;
        Disclaimer = disclaimer;
        Guidance = guidance;
        TemplateVersion = templateVersion;
        TemplateDigest = templateDigest;
        Templates = templates;
        Packages = packages;
        Warnings = warnings;
        UnavailableSections = unavailableSections;
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
    }
    public string SchemaVersion { get; }
    public string Status { get; }
    public string Disclaimer { get; }
    public GuidanceSnapshot Guidance { get; }
    public string TemplateVersion { get; }
    public string TemplateDigest { get; }
    public ImmutableArray<FixTemplate> Templates { get; }
    public ImmutableArray<FixPackage> Packages { get; }
    public ImmutableArray<string> Warnings { get; }
    public ImmutableArray<string> UnavailableSections { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}

public sealed record FixPackageResult(FixPackageIssue? Issue, FixPackageSnapshot? Snapshot)
{
    public bool Succeeded => Issue is null && Snapshot is not null;
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SyntheticEvaluation;
using SyntheticEvaluationWorkflow;
using SyntheticEvaluationWorkflowFixtures;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    private static int Main()
    {
        try { Run(); Console.WriteLine($"PASS {checks} synthetic evaluation workflow unit assertions."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Run()
    {
        var seed = FictionalEvaluationFixture.BuildSeed();
        var admitted = EvaluationWorkflowPolicy.Seed(seed);
        Check(admitted.Sample.Selected.Count == 100 && admitted.Sample.Population.Count == 120, "frozen100/120");
        Check(admitted.Sample.PopulationDigest == "069dd6f848250dfa46be775b382d563f7979e15e6349a29f59d4770a55955aa1", "independent populationgolden");
        Check(admitted.Sample.ContentDigest == "5a372640cd648d266cf0112f69e2510aa3751cc81d6e61c3321870461d0ea110", "independent samplegolden");
        Check(admitted.Sample.Selected.Count(s => s.Mandatory) == 40 && admitted.Sample.Strata.Select(s => s.Allocation).SequenceEqual(new[] { 15, 45 }), "mandatory+Hamilton");
        var first = seed.Originals[0];
        var sourceDigest = EvaluationWorkflowCanonical.Hash(EvaluationWorkflowCanonical.Json(admitted.Source));
        var good = new EvaluationWorkflowCommand(Guid.Parse("bb0ef1e1-b759-4b3a-a8cb-d8ca770c33be"), first.MemberId,
            EvaluationWorkflowCommandKind.Review, 0, 0, seed.Registry.VersionId, sourceDigest, admitted.Sample.ContentDigest,
            EvaluationReviewOutcome.Confirmed, null, "Independent fictional conclusion.", first.EvidenceReferenceIds, null);
        Check(EvaluationWorkflowPolicy.Command(good), "validcommand");
        var invalid = new List<EvaluationWorkflowCommand?>
        {
            null,good with{EventId=Guid.Empty},good with{MemberId="real-id"},good with{Kind=(EvaluationWorkflowCommandKind)99},
            good with{ExpectedAggregateRevision=-1},good with{ExpectedMemberRevision=9007199254740992},
            good with{ExpectedRegistryVersionId="bad"},good with{ExpectedSourceDigest=new('F',64)},
            good with{ExpectedSampleDigest=new('a',63)},good with{Reason=""},good with{Reason="  "},
            good with{Reason="bad\0text"},good with{Reason=new('x',2001)},good with{Reason="\ud800"},
            good with{Reason="\udfff"},good with{EvidenceReferenceIds=[]},
            good with{EvidenceReferenceIds=Enumerable.Repeat(first.EvidenceReferenceIds[0],17).ToArray()},
            good with{EvidenceReferenceIds=[first.EvidenceReferenceIds[0],first.EvidenceReferenceIds[0]]},
            good with{Outcome=EvaluationReviewOutcome.Unreviewed},good with{Outcome=(EvaluationReviewOutcome)42},
            good with{OriginatingClassification=EvaluationOriginClassification.Rejected},
            good with{Correction=new("Medium",null,null,null)},
            good with{Outcome=EvaluationReviewOutcome.Corrected},
            good with{Outcome=EvaluationReviewOutcome.Corrected,OriginatingClassification=EvaluationOriginClassification.Rejected,Correction=new(null,null,null,null)},
            good with{Kind=EvaluationWorkflowCommandKind.PresentationCorrection,Correction=new("Medium",null,null,null)}
        };
        foreach (var c in invalid) Check(!EvaluationWorkflowPolicy.Command(c), "invalidadmission");
        Check(EvaluationWorkflowPolicy.Command(good with { Reason = new('x', 2000) }), "2000boundary");
        Check(EvaluationWorkflowPolicy.Command(good with { Reason = "\ud83d\ude00" }), "validsurrogatepair");
        Check(EvaluationWorkflowPolicy.Command(good with { Reason = "\ufffd" }), "explicitreplacementvalid");
        Check(!EvaluationWorkflowPolicy.Command(good with { Reason = "\ud800" }) && EvaluationWorkflowPolicy.Command(good with { Reason = "\ufffd" }), "lonesurrogatecannotcollapsecanonicalreplay");
        var references = new[] { first.EvidenceReferenceIds[0] };
        var detached = EvaluationWorkflowPolicy.Detach(good with { EvidenceReferenceIds = references });
        references[0] = "synthetic-changed";
        Check(detached.EvidenceReferenceIds[0] == first.EvidenceReferenceIds[0], "commanddetached");
        var corrections = new EvaluationWorkflowCorrection("Medium", "Fictional category", "Independent cause", "Revised recommendation");
        Check(EvaluationWorkflowPolicy.Command(good with { Outcome = EvaluationReviewOutcome.Corrected, OriginatingClassification = EvaluationOriginClassification.Rejected, Correction = corrections }), "fourdimensions");
        var states = new[] { EvaluationReviewOutcome.Unreviewed, EvaluationReviewOutcome.Indeterminate, EvaluationReviewOutcome.Confirmed, EvaluationReviewOutcome.Rejected, EvaluationReviewOutcome.Corrected };
        foreach (var state in states)
        {
            var previous = new WorkflowCurrent(first.MemberId, 4, state, state == EvaluationReviewOutcome.Corrected ? EvaluationOriginClassification.Rejected : null, null);
            var presentation = good with { Kind = EvaluationWorkflowCommandKind.PresentationCorrection, Outcome = null, OriginatingClassification = null, Correction = corrections, EvidenceReferenceIds = [] };
            var updated = EvaluationWorkflowPolicy.Apply(previous, presentation);
            Check(updated.Revision == 5 && updated.Correction == corrections, "presentationrevision+dimensions");
            Check(updated.Outcome == (state is EvaluationReviewOutcome.Confirmed or EvaluationReviewOutcome.Rejected ? EvaluationReviewOutcome.Corrected : state), "presentationcannotmanufactureclassification");
            Check(updated.OriginatingClassification == (state == EvaluationReviewOutcome.Confirmed ? EvaluationOriginClassification.Confirmed : state is EvaluationReviewOutcome.Rejected or EvaluationReviewOutcome.Corrected ? EvaluationOriginClassification.Rejected : null), "preserveorigin");
        }
        foreach (var context in new[] { true, false })
            foreach (var outcome in new[] { EvaluationReviewOutcome.Confirmed, EvaluationReviewOutcome.Rejected, EvaluationReviewOutcome.Indeterminate, EvaluationReviewOutcome.Corrected })
                Check(EvaluationWorkflowPolicy.Context(good with { Outcome = outcome }, new(null, context)) == (context == (outcome != EvaluationReviewOutcome.Indeterminate)), "exactnewcontextcompatibility");
        Check(!EvaluationWorkflowPolicy.Context(good, new(EvaluationReviewerIssue.ConflictDenied, null)), "deniednevercontext");
        var normalized = EvaluationWorkflowCanonical.Registry(seed.Registry);
        var next = normalized with { VersionId = "synthetic-registry-v2", Assignments = normalized.Assignments.Select((a, i) => i == 0 ? a with { Revision = 2, RelatedHistoryGranted = false } : a).ToArray() };
        Check(EvaluationWorkflowPolicy.RegistryReplacement(normalized, normalized, next), "grantrevocationwithinceiling");
        Check(!EvaluationWorkflowPolicy.RegistryReplacement(normalized, normalized, next with { Assignments = next.Assignments.Select(a => a with { Revision = 1 }).ToArray() }), "changedrecordmustbumprevision");
        Check(!EvaluationWorkflowPolicy.RegistryReplacement(normalized, normalized, next with { Assignments = next.Assignments.Select(a => a with { Role = EvaluationReviewerRole.Auditor }).ToArray() }), "noroleexpansion");
        Check(!EvaluationWorkflowPolicy.RegistryReplacement(normalized, normalized, next with { Assignments = next.Assignments.Select(a => a with { ReviewCategoryIds = ["synthetic-new-category"] }).ToArray() }), "nocategoryexpansion");
        var at = new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);
        var json = EvaluationWorkflowCanonical.Json(normalized);
        var registry = new WorkflowRegistry(1, normalized, json, EvaluationWorkflowCanonical.Hash(json), at);
        var members = seed.Originals.ToDictionary(o => o.MemberId, o => new WorkflowCurrent(o.MemberId, 0, EvaluationReviewOutcome.Unreviewed, null, null));
        var version = EvaluationWorkflowCanonical.Version(admitted.Source, admitted.Sample, sourceDigest, 0, registry, [], members, at, null);
        using var manifest = JsonDocument.Parse(version.VersionManifestJson);
        var names = manifest.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Check(names.SequenceEqual(new[] { "schemaVersion", "version", "sourceDigest", "populationDigest", "sampleDigest", "originalSampleVersionManifestDigest", "originalSampleCorrectionCutoffUtc", "registryVersionId", "registryDigest", "lastEventSequence", "eventsDigest", "correctionCutoffUtc", "predecessorDigest" }), "independentexactmanifestfieldorder");
        Check(manifest.RootElement.GetProperty("schemaVersion").GetString() == "synthetic-evaluation-workflow-version-v1", "manifestversion");
        Check(manifest.RootElement.GetProperty("eventsDigest").GetString() == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("[]"))), "independentempty-eventdigest");
        Check(version.PredecessorDigest is null && version.Version == 0 && version.LastEventSequence == 0, "genesis");
        using var warning = JsonDocument.Parse(version.WarningCanonicalJson);
        var general = warning.RootElement.GetProperty("summaries").GetProperty("generalAi");
        Check(general.GetProperty("selected").GetInt32() == 100 &&
            general.GetProperty("unreviewed").GetInt32() == 100 &&
            general.GetProperty("denominator").GetInt32() == 0 && general.GetProperty("lowSampleWarning").GetBoolean(), "literalinitialcounts");
        Check(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(version.SnapshotCanonicalJson))) == version.ContentDigest, "independentrawsnapshothash");
        using var envelope = JsonDocument.Parse(version.SnapshotCanonicalJson);
        Check(envelope.RootElement.GetProperty("versionManifestJson").GetString() == version.VersionManifestJson &&
            envelope.RootElement.GetProperty("accuracyCanonicalJson").GetString() == version.AccuracyCanonicalJson &&
            envelope.RootElement.GetProperty("warningCanonicalJson").GetString() == version.WarningCanonicalJson, "snapshotrawrelations");
        foreach (var input in new[] { "Host=remote;Database=iga_synthetic_evaluation_bad", "Host=127.0.0.1;Database=real", "Host=127.0.0.1,remote;Database=iga_synthetic_evaluation_bad" })
        {
            var rejected = false; try { _ = new SyntheticEvaluationWorkflowStore(input); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "connectionguard");
        }
    }
}

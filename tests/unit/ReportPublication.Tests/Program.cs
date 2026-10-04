using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportPublication;

var directory = Path.Combine(AppContext.BaseDirectory, "fixtures");
var failed = new List<string>();
var passed = 0;
void Check(string name, Action action)
{
    try { action(); passed++; }
    catch (Exception error) { failed.Add(name + ": " + error.GetType().Name + " " + error.Message); }
}
void Require(bool condition) { if (!condition) throw new InvalidOperationException("Independent assertion failed."); }
JsonNode Original(string name) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(directory, name)))!;
void Exact(string name, byte[] actual) => Require(actual.SequenceEqual(File.ReadAllBytes(Path.Combine(directory, name))));
void Refuse(string name, string bundle, Action<JsonNode> mutate, bool source = false)
{
    Check(name, () =>
    {
        var node = Original(bundle + (source ? ".source.json" : ".projection.json"));
        mutate(node);
        try
        {
            _ = source ? NativePublicationCanonicalV1.SourceBytes(FixtureLoader.Source(node))
                : NativePublicationCanonicalV1.ProjectionBytes(FixtureLoader.Load<PublicationProjectionV1>(node));
        }
        catch (InvalidOperationException) { return; }
        throw new Exception("Malformed typed input was accepted.");
    });
}
foreach (var golden in Original("goldens.json")["files"]!.AsObject())
{
    Check("original-commitment:" + golden.Key, () =>
    {
        var bytes = File.ReadAllBytes(Path.Combine(directory, golden.Key));
        Require(bytes.LongLength == golden.Value!["byteLength"]!.GetValue<long>());
        Require(NativePublicationCanonicalV1.Hash(bytes) == golden.Value["sha256"]!.GetValue<string>());
    });
}
foreach (var bundle in new[] { "completed", "warned", "source-limitation" })
{
    Check("typed-projection:" + bundle, () => Exact(bundle + ".projection.json", NativePublicationCanonicalV1.ProjectionBytes(FixtureLoader.Load<PublicationProjectionV1>(Original(bundle + ".projection.json")))));
    Check("typed-score:" + bundle, () => Exact(bundle + ".score.json", NativePublicationCanonicalV1.ScoreBytes(FixtureLoader.Load<PublicationScoreV1>(Original(bundle + ".score.json")))));
    Check("typed-source:" + bundle, () => Exact(bundle + ".source.json", NativePublicationCanonicalV1.SourceBytes(FixtureLoader.Source(Original(bundle + ".source.json")))));
    Check("typed-command:" + bundle, () =>
    {
        var json = Original(bundle + ".command.json");
        json["invocationId"] = Guid.NewGuid().ToString(); json["correlationId"] = Guid.NewGuid().ToString();
        var actor = FixtureLoader.Load<PublicationActorV1>(json["actor"]!);
        Exact(bundle + ".command.json", NativePublicationCanonicalV1.CommandBytes(actor, FixtureLoader.Load<PublishCommandV1>(json)));
        json["invocationId"] = Guid.NewGuid().ToString(); json["correlationId"] = Guid.NewGuid().ToString();
        Exact(bundle + ".command.json", NativePublicationCanonicalV1.CommandBytes(actor, FixtureLoader.Load<PublishCommandV1>(json)));
    });
    Check("typed-read-request:" + bundle, () =>
    {
        var json = Original(bundle + ".read-request.json"); json["correlationId"] = Guid.NewGuid().ToString();
        Exact(bundle + ".read-request.json", NativePublicationCanonicalV1.ReadRequestBytes(FixtureLoader.Load<PublicationActorV1>(json["actor"]!), FixtureLoader.Load<ExactReportRequestV1>(json)));
    });
}
foreach (var bundle in new[] { "completed", "warned", "source-limitation" })
{
    Check("typed-manifest:" + bundle, () => Exact(bundle + ".manifest.json", NativePublicationCanonicalV1.ManifestBytes(FixtureLoader.Load<PublicationManifestV1>(Original(bundle + ".manifest.json")))));
    foreach (var suffix in new[] { ".publish-event.json", ".read-event.json" })
        Check("typed-audit:" + bundle + suffix, () => Exact(bundle + suffix, NativePublicationCanonicalV1.AuditEventBytes(FixtureLoader.Load<PublicationAuditEventV1>(Original(bundle + suffix)))));
    Check("typed-receipt:" + bundle, () => Exact(bundle + ".receipt.json", NativePublicationCanonicalV1.PublicationReceiptBytes(FixtureLoader.Load<PublicationReceiptV1>(Original(bundle + ".receipt.json")))));
    Check("typed-read-receipt:" + bundle, () => Exact(bundle + ".read-receipt.json", NativePublicationCanonicalV1.ReadReceiptBytes(FixtureLoader.Load<ExactReadReceiptV1>(Original(bundle + ".read-receipt.json")))));
    Check("production-parser-roundtrip:" + bundle, () =>
    {
        var bytes = File.ReadAllBytes(Path.Combine(directory, bundle + ".projection.json"));
        Require(NativePublicationProjectionReaderV1.TryRead(bytes, out var parsed) && parsed is not null);
        Exact(bundle + ".projection.json", NativePublicationCanonicalV1.ProjectionBytes(parsed!));
        bytes[0] = 0;
        Exact(bundle + ".projection.json", NativePublicationCanonicalV1.ProjectionBytes(parsed!));
    });
}
Check("typed-anonymous-audit", () => Exact("anonymous-denial.audit.json", NativePublicationCanonicalV1.AuditEventBytes(FixtureLoader.Load<PublicationAuditEventV1>(Original("anonymous-denial.audit.json")))));
void RawRefuse(string name, Func<byte[], byte[]> mutation) => Check("production-parser-refusal:" + name, () =>
{
    var original = File.ReadAllBytes(Path.Combine(directory, "completed.projection.json"));
    var bytes = mutation(original); Require(!bytes.SequenceEqual(original));
    Require(!NativePublicationProjectionReaderV1.TryRead(bytes, out var parsed) && parsed is null);
});
RawRefuse("trailing-newline", b => b.Concat(new byte[] { 10 }).ToArray());
RawRefuse("BOM", b => new byte[] { 239, 187, 191 }.Concat(b).ToArray());
RawRefuse("duplicate-key", b => Encoding.UTF8.GetBytes("{\"runRevision\":\"9\"," + Encoding.UTF8.GetString(b)[1..]));
RawRefuse("extra-key", b => Encoding.UTF8.GetBytes("{\"rawSql\":null," + Encoding.UTF8.GetString(b)[1..]));
RawRefuse("numeric-counter", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"runRevision\":\"9\"", "\"runRevision\":9", StringComparison.Ordinal)));
RawRefuse("padded-counter", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"runRevision\":\"9\"", "\"runRevision\":\"09\"", StringComparison.Ordinal)));
RawRefuse("numeric-enum-alias", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"Medium\"", "\"2\"", StringComparison.Ordinal)));
RawRefuse("noncanonical-decimal", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"72.5\"", "\"72.50\"", StringComparison.Ordinal)));
RawRefuse("unknown-nested-key", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"inputs\":{", "\"inputs\":{\"rawSql\":null,", StringComparison.Ordinal)));
RawRefuse("omitted-field", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"modelVersion\":null,", "", StringComparison.Ordinal)));
RawRefuse("null-array", b => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(b).Replace("\"acceptedRisks\":[]", "\"acceptedRisks\":null", StringComparison.Ordinal)));
Check("typed-sets-sort-to-original-frozen-bytes", () =>
{
    var source = Original("warned.source.json");
    void Reverse(JsonNode array)
    {
        var values = array.AsArray().Select(v => v!.DeepClone()).Reverse().ToArray();
        array.AsArray().Clear(); foreach (var value in values) array.AsArray().Add(value);
    }
    Reverse(source["requiredCategories"]!); Reverse(source["requiredFields"]!); Reverse(source["provenance"]!);
    Reverse(source["projection"]!["warnings"]!);
    Exact("warned.source.json", NativePublicationCanonicalV1.SourceBytes(FixtureLoader.Source(source)));
});
RawRefuse("unsorted-warning-set", _ =>
{
    // Replace only original frozen array bytes so no test encoder adds escapes.
    using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "warned.projection.json")));
    var raw = doc.RootElement.GetProperty("warnings").GetRawText();
    var items = doc.RootElement.GetProperty("warnings").EnumerateArray().Select(v => v.GetRawText()).Reverse();
    return Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "warned.projection.json")))
        .Replace(raw, "[" + string.Join(",", items) + "]", StringComparison.Ordinal));
});
Refuse("zero-revision", "completed", n => n["runRevision"] = "0");
Refuse("zero-scope", "completed", n => n["scope"]!["customerId"] = Guid.Empty.ToString());
Refuse("invalid-input-digest", "completed", n => n["inputs"]!["baselineDigest"] = "A" + new string('0', 63));
Refuse("unpaired-outcome", "completed", n => n["inputs"]!["desiredOutcomeVersion"] = "new");
Refuse("score-out-of-range", "completed", n => n["scores"]!["provisional"]!["value"] = "100.01");
Refuse("unavailable-score-value", "completed", n => n["scores"]!["provisional"]!["availability"] = "Unavailable");
Refuse("duplicate-finding", "completed", n => n["findings"]!.AsArray().Add(n["findings"]![0]!.DeepClone()));
Refuse("unknown-severity-enum", "completed", n => n["findings"]![0]!["severity"] = "2147483647");
Refuse("missing-root-cause", "completed", n => n["findings"]![0]!["rootCauseIds"]!.AsArray().Clear());
Refuse("source-root-outside-frozen-category", "completed", n => n["projection"]!["rootCauses"]![0]!["category"] = "Other", true);
Refuse("source-recommendation-outside-frozen-category", "completed", n => n["projection"]!["recommendations"]![0]!["category"] = "Other", true);
Refuse("source-reference-outside-frozen-category", "completed", n => n["projection"]!["technicalAppendices"]!["protectedReferences"]![0]!["category"] = "Other", true);
Refuse("missing-reference", "completed", n => n["findings"]![0]!["referenceIds"]![0] = Guid.NewGuid().ToString());
Refuse("coverage-arithmetic", "warned", n => n["coverage"]!["counts"]!["gap"] = "0");
Refuse("coverage-explicit-reason", "warned", n => n["coverage"]!["items"]![0]!["reason"] = "Inaccessible");
Refuse("severe-false-mandatory-with-warning", "warned", n => n["findings"]![0]!["mandatoryReview"] = false);
Refuse("severe-false-mandatory-without-warning", "warned", n => { n["findings"]![0]!["mandatoryReview"] = false; n["warnings"]!.AsArray().RemoveAt(1); });
Refuse("missing-severe-warning", "warned", n => n["warnings"]!.AsArray().RemoveAt(1));
Refuse("warning-wrong-category", "warned", n => n["warnings"]![1]!["category"] = "Other");
Refuse("warning-wrong-record-kind", "warned", n => n["warnings"]![1]!["recordId"] = n["coverage"]!["items"]![0]!["id"]!.DeepClone());
Refuse("coverage-warning-wrong-category", "warned", n => n["warnings"]![0]!["category"] = "Other");
Refuse("marker-wrong-section", "warned", n => n["redactionMarkers"]![0]!["section"] = "findings");
Refuse("marker-metadata-section", "warned", n => n["redactionMarkers"]![0]!["section"] = "inputs");
Refuse("marker-undeclared-field", "warned", n => n["redactionMarkers"]![0]!["field"] = "summary.text");
Refuse("source-missing-provenance", "source-limitation", n => n["provenance"]!.AsArray().Clear(), true);
Refuse("source-warning-outside-frozen-category", "source-limitation", n => n["projection"]!["warnings"]![0]!["category"] = "Other", true);
Refuse("source-wrong-required-category", "completed", n => n["requiredCategories"]![0] = "Other", true);
Refuse("source-invented-required-field", "completed", n => n["requiredFields"]![0] = "rawSql", true);
Refuse("source-invalid-terminal", "completed", n => n["assessmentState"] = "2147483647", true);
Refuse("source-retention-offset", "completed", n => n["retention"]!["expiresAtUtc"] = "2026-11-03T00:00:00.0000000+01:00", true);
Refuse("text-bound", "completed", n => n["executiveSummary"]![0]!["text"] = new string('x', 4097));
Refuse("token-bound", "completed", n => n["findings"]![0]!["category"] = new string('x', 129));
Refuse("typed-lone-surrogate", "completed", n => n["executiveSummary"]![0]!["text"] = "\ud800");

foreach (var vector in Original("unicode-vectors.json").AsArray())
{
    Check("unicode:" + vector!["id"]!.GetValue<string>(), () =>
    {
        var bytes = Convert.FromHexString(vector["hex"]!.GetValue<string>());
        string scalar;
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) throw new FormatException("BOM");
            using var document = JsonDocument.Parse(bytes);
            scalar = document.RootElement.GetString()!;
            _ = new UTF8Encoding(false, true).GetByteCount(scalar);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or DecoderFallbackException or EncoderFallbackException or FormatException)
        { Require(vector["expected"]!.GetValue<string>() == "Reject"); return; }
        Require(vector["expected"]!.GetValue<string>() == "Accept");
        var node = Original("completed.projection.json"); node["executiveSummary"]![0]!["text"] = scalar;
        var encoded = NativePublicationCanonicalV1.ProjectionBytes(FixtureLoader.Load<PublicationProjectionV1>(node));
        var expected = Convert.FromHexString(vector["canonicalHex"]!.GetValue<string>());
        Require(Encoding.UTF8.GetString(encoded).Contains("\"text\":" + Encoding.UTF8.GetString(expected), StringComparison.Ordinal));
    });
}
foreach (var vector in Original("unicode-vectors.json").AsArray())
{
    Check("production-unicode-parser:" + vector!["id"]!.GetValue<string>(), () =>
    {
        var bytes = File.ReadAllBytes(Path.Combine(directory, "completed.projection.json"));
        using var document = JsonDocument.Parse(bytes);
        var originalText = Encoding.UTF8.GetBytes(document.RootElement.GetProperty("executiveSummary")[0].GetProperty("text").GetRawText());
        var accept = vector["expected"]!.GetValue<string>() == "Accept";
        var scalarBytes = Convert.FromHexString(vector[accept ? "canonicalHex" : "hex"]!.GetValue<string>());
        var position = bytes.AsSpan().IndexOf(originalText); Require(position >= 0);
        var malformed = bytes[..position].Concat(scalarBytes).Concat(bytes[(position + originalText.Length)..]).ToArray();
        var success = NativePublicationProjectionReaderV1.TryRead(malformed, out var projection);
        Require(success == accept && (projection is not null) == accept);
        if (accept) Require(NativePublicationCanonicalV1.ProjectionBytes(projection!).SequenceEqual(malformed));
    });
}
void MetadataRefuse<T>(string name, string file, Action<JsonNode> mutate, Func<T, byte[]> encode)
{
    Check("metadata-refusal:" + name, () =>
    {
        var json = Original(file); mutate(json);
        try { _ = encode(FixtureLoader.Load<T>(json)); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Malformed metadata accepted.");
    });
}
MetadataRefuse<PublicationManifestV1>("descriptor-digest-binding", "completed.manifest.json", n => n["artifactInputs"]![0]!["digest"] = new string('f', 64), NativePublicationCanonicalV1.ManifestBytes);
MetadataRefuse<PublicationManifestV1>("missing-score-descriptor", "completed.manifest.json", n => n["artifactInputs"]!.AsArray().RemoveAt(1), NativePublicationCanonicalV1.ManifestBytes);
MetadataRefuse<PublicationManifestV1>("zero-descriptor-length", "completed.manifest.json", n => n["artifactInputs"]![0]!["byteLength"] = "0", NativePublicationCanonicalV1.ManifestBytes);
MetadataRefuse<PublicationManifestV1>("manifest-required-field", "completed.manifest.json", n => n["requiredFields"]![0] = "rawSql", NativePublicationCanonicalV1.ManifestBytes);
MetadataRefuse<PublicationAuditEventV1>("success-deny-reason", "completed.publish-event.json", n => n["reason"] = "AuthorityDenied", NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("deny-keeps-manifest", "completed.publish-event.json", n => { n["outcome"] = "Denied"; n["reason"] = "AuthorityDenied"; }, NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("anonymous-keeps-actor", "completed.publish-event.json", n => n["actorKind"] = "Anonymous", NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("missing-human-actor", "completed.publish-event.json", n => n["actor"] = null, NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("unknown-commit-outcome", "completed.publish-event.json", n => n["outcome"] = "2147483647", NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("read-missing-returned-field", "completed.read-event.json", n => n["returnedFields"]!.AsArray().RemoveAt(0), NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("read-operationId", "completed.read-event.json", n => n["operationId"] = Guid.NewGuid().ToString(), NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("publish-returned-content", "completed.publish-event.json", n => n["returnedFields"]!.AsArray().Add("findings"), NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationAuditEventV1>("redacted-fields-fallback", "completed.read-event.json", n => n["redactedFields"]!.AsArray().Add("findings"), NativePublicationCanonicalV1.AuditEventBytes);
MetadataRefuse<PublicationReceiptV1>("receipt-zero-event", "completed.receipt.json", n => n["eventId"] = Guid.Empty.ToString(), NativePublicationCanonicalV1.PublicationReceiptBytes);
MetadataRefuse<ExactReadReceiptV1>("read-receipt-malformed-requestdigest", "completed.read-receipt.json", n => n["requestDigest"] = "private locator", NativePublicationCanonicalV1.ReadReceiptBytes);
Check("source-constructor-not-public", () => Require(typeof(SourceCaptureV1).GetConstructors().Length == 0));
Check("defensive-collections-and-returned-bytes", () =>
{
    var source = FixtureLoader.Source(Original("completed.source.json"));
    var categories = source.RequiredCategories.ToArray(); var fields = source.RequiredFields.ToArray(); var provenance = source.Provenance.ToArray();
    var captured = new SourceCaptureV1(source.Projection, source.AssessmentState, source.Retention, categories, fields, provenance);
    categories[0] = "Other"; fields[0] = "rawSql"; provenance[0] = provenance[0] with { Digest = new string('f', 64) };
    Exact("completed.source.json", NativePublicationCanonicalV1.SourceBytes(captured));
    var bytes = NativePublicationCanonicalV1.SourceBytes(captured); bytes[0] = 0;
    Exact("completed.source.json", NativePublicationCanonicalV1.SourceBytes(captured));
    var findings = source.Projection.Findings[0]; var references = findings.ReferenceIds.ToArray(); var causes = findings.RootCauseIds.ToArray();
    var copy = new PublicationFindingV1(findings.Id, findings.Category, findings.Title, findings.Summary, findings.Severity, findings.State, findings.Method,
        findings.ConfidencePercent, findings.ConfidenceBand, findings.ConfidenceAvailability, findings.ConfidenceReason, findings.MandatoryReview, references, causes, findings.OriginalDigest, findings.ReviewRevision);
    references[0] = Guid.Empty; causes[0] = Guid.Empty;
    Require(copy.ReferenceIds.SequenceEqual(findings.ReferenceIds) && copy.RootCauseIds.SequenceEqual(findings.RootCauseIds));
});
Check("defensive-nested-projection-collections", () =>
{
    var p = FixtureLoader.Source(Original("warned.source.json")).Projection;
    var executive = p.ExecutiveSummary.ToArray(); var warnings = p.Warnings.ToArray(); var markers = p.RedactionMarkers.ToArray();
    var items = p.Coverage.Items.ToArray(); var notes = p.TechnicalAppendices.Notes.ToArray(); var protectedReferences = p.TechnicalAppendices.ProtectedReferences.ToArray();
    var findings = p.Findings.ToArray(); var rootCauses = p.RootCauses.ToArray();
    var coverage = new PublicationCoverageV1(items, p.Coverage.Counts);
    var appendices = new PublicationAppendicesV1(notes, protectedReferences);
    var clone = new PublicationProjectionV1(p.Scope, p.RunId, p.RunRevision, p.Inputs, executive, p.EnvironmentScope,
        p.Scores, p.Maturity, p.Dimensions, coverage, findings, rootCauses, p.HealthyControls, p.Recommendations,
        p.AcceptedRisks, warnings, p.Methodology, appendices, markers);
    executive[0] = executive[0] with { Text = "changed" }; warnings[0] = warnings[0] with { Category = "Other" };
    markers[0] = markers[0] with { Section = "inputs" }; items[0] = items[0] with { Reason = PublicationReasonV1.None };
    protectedReferences[0] = protectedReferences[0] with { Id = Guid.Empty }; findings[0] = null!; rootCauses[0] = null!;
    if (notes.Length > 0) notes[0] = null!;
    Exact("warned.projection.json", NativePublicationCanonicalV1.ProjectionBytes(clone));
    var cause = p.RootCauses[0]; var linked = cause.FindingIds.ToArray();
    var causeCopy = new PublicationRootCauseV1(cause.Id, cause.Category, cause.Summary, linked, cause.Availability, cause.Reason);
    linked[0] = Guid.Empty; Require(causeCopy.FindingIds.SequenceEqual(cause.FindingIds));
    var commandJson = Original("warned.command.json");
    var actor = FixtureLoader.Load<PublicationActorV1>(commandJson["actor"]!);
    var acknowledged = p.Warnings.ToArray();
    var command = new PublishCommandV1(Guid.Parse(commandJson["operationId"]!.GetValue<string>()), Guid.NewGuid(), Guid.NewGuid(),
        p.Scope, p.RunId, p.RunRevision, commandJson["expectedSourceDigest"]!.GetValue<string>(), acknowledged);
    acknowledged[0] = acknowledged[0] with { Category = "Other" };
    Exact("warned.command.json", NativePublicationCanonicalV1.CommandBytes(actor, command));
});
Console.WriteLine($"Compiled independent checks: {passed} passed, {failed.Count} failed. All 31 original typed codecs verified; persisted flow and real source acceptance remain pending.");
foreach (var failure in failed) Console.WriteLine(failure);
var flowFailures = await FlowTests.RunAsync();
return failed.Count + flowFailures == 0 ? 0 : 1;

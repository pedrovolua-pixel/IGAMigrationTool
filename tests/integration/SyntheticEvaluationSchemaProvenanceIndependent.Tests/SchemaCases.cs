using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentRuns;
using Npgsql;
using SyntheticAiExecution;
using SyntheticEvaluation;
using SyntheticEvaluationSchemaProvenanceIntegration;
using SyntheticOutcomePriority;

internal static partial class Program
{
    private static string Reference(string kind, params string[] parts)
    {
        using var bytes = new MemoryStream();
        bytes.Write(Encoding.ASCII.GetBytes("iga.synthetic-evaluation.native-reference.v1\0"));
        void Field(string value)
        {
            var data = new UTF8Encoding(false, true).GetBytes(value);
            Span<byte> size = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(size, checked((uint)data.Length));
            bytes.Write(size); bytes.Write(data);
        }
        foreach (var value in new[] { kind, "synthetic-customer", "synthetic-project", "synthetic-environment" }) Field(value);
        Span<byte> count = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)parts.Length)); bytes.Write(count);
        foreach (var value in parts) Field(value);
        return "synthetic-ref-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()));
    }
    private static string Golden(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));
    private static void PortableGoldens()
    {
        using var values = JsonDocument.Parse(Golden("canonical-literals.json"));
        var v = values.RootElement;
        foreach (var (file, field) in new[] { ("normal-proof-golden.json", "proofDigest"), ("normal-readiness-representative-golden.json", "readinessDigest") })
        {
            var raw = Golden(file); using var doc = JsonDocument.Parse(raw);
            Check(Hash(raw) == v.GetProperty(field).GetString(), "preimplementation full canonical SHA literal " + field);
            foreach (var culture in new[] { "en-US", "fr-FR", "tr-TR" })
            {
                var previous = CultureInfo.CurrentCulture;
                try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); Check(Canonical(doc.RootElement) == raw, "independent Python/C# canonical culture parity " + culture); }
                finally { CultureInfo.CurrentCulture = previous; }
            }
        }
        Check(v.GetProperty("literalBundleKeys").GetInt32() == 17, "fixed17 concrete Q keys pinned before author consumption");
        var value = v.GetProperty("aiSchemaValueJson").GetString()!;
        Check(Reference("version-binding", "AiSchema", value) == v.GetProperty("reference").GetString(), "literal independent schema alias framing/hash parity");
        using var bundle = JsonDocument.Parse(value);
        Check(bundle.RootElement.GetProperty("Q.$.works[0].missingReason").ValueKind == JsonValueKind.Null, "successful contextual null retained in schema bundle");
        Check(bundle.RootElement.GetProperty("Q.$.works[0].inputSchemaVersion").GetString() == "synthetic-ai-fixture-input-v1" &&
            bundle.RootElement.GetProperty("Q.$.works[0].accepted.outputSchemaVersion").GetString() == "synthetic-ai-fixture-output-v1" &&
            bundle.RootElement.GetProperty("Q.$.works[0].accepted.acceptedSnapshotSchemaVersion").GetString() == "synthetic-ai-proposal-snapshot-v1", "three distinct native schema literals");
    }
    private static async Task GuardCases()
    {
        var b = new NpgsqlConnectionStringBuilder(Database);
        foreach (var name in new[] { "unowned", Prefix + new string('a', 64), Prefix + "bad-name", Prefix + "bad\"name" })
        {
            b.Database = name; var lookups = CatalogLookups; var creates = DatabaseCreates;
            try { await CreateFreshDatabase(b.ConnectionString); throw new InvalidOperationException("invalid name admitted"); }
            catch (InvalidOperationException x) when (x.Message == "Dedicated independent synthetic database required.")
            { Check(CatalogLookups == lookups && DatabaseCreates == creates, "invalid database name refused before any catalog/CREATE command"); }
        }
    }
    private static async Task Uninitialized()
    {
        var creates = DatabaseCreates;
        try { await CreateFreshDatabase(); throw new InvalidOperationException("empty existing DB admitted"); }
        catch (InvalidOperationException x) when (x.Message == "Independent database must be fresh; existing data preserved.")
        { Check(DatabaseCreates == creates, "existing empty selected database preserved and refused"); }
        await using var c = await Open();
        await using (var t = await c.BeginTransactionAsync())
        {
            OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, Guid.NewGuid(), new string('0', 64)), AiIssue.NotInitialized, "uninitialized owning schema reader closed");
            await t.RollbackAsync();
        }
        await using (var t = await c.BeginTransactionAsync())
        {
            Denied(await Adapter.CaptureAsync(c, t, Guid.NewGuid(), ConsultantAi, ConsultantOutcome), Phase1BAcceptedSchemaIssue.NotInitialized, "uninitialized composed capture closed");
            await t.RollbackAsync();
        }
    }
    private static void OwnerDenied(AiOperationResult<AiAcceptedSchemaProof> result, AiIssue issue, string label) => Check(result.Issue == issue && result.Value is null, label);
    private static async Task<string> SourceDigest(Guid run)
    {
        await using var c = await Open(); await using var t = await c.BeginTransactionAsync();
        var read = await Ai.ReadInTransactionAsync(c, t, ConsultantAi, run);
        Check(read.Succeeded && read.Value is not null, "unchanged owning read supplies expected current source digest");
        await t.RollbackAsync(); return read.Value!.ContentDigest;
    }
    private static async Task<AiOperationResult<AiAcceptedSchemaProof>> OwnerRead(Guid run, string digest, AiAuthority? authority = null)
    {
        await using var c = await Open(); await using var t = await c.BeginTransactionAsync();
        var result = await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, authority ?? ConsultantAi, run, digest);
        Check(t.Connection == c, "owning proof reader retains caller transaction");
        await t.RollbackAsync(); return result;
    }
    private static async Task DirectOwnerMatrix(SyntheticRunSnapshot run)
    {
        var digest = await SourceDigest(run.RunId); var before = await Rows();
        foreach (var authority in new[] { ConsultantAi, Worker, ConsultantAi with { Actions = [AiAction.Read, AiAction.Read, AiAction.Override], Categories = ["OPERATIONS", "OPERATIONS"] } })
            Check((await OwnerRead(run.RunId, digest, authority)).Value is not null, "unchanged direct owner Read policy and allowed action/category duplicates");
        AiAuthority[] bad = [ConsultantAi with { Roles = [] }, ConsultantAi with { Roles = default }, ConsultantAi with { Roles = [AiRole.Consultant, AiRole.Consultant] }, ConsultantAi with { Roles = [AiRole.Consultant, AiRole.Worker] }, ConsultantAi with { Roles = [(AiRole)999] }, ConsultantAi with { Roles = [AiRole.Auditor] }, ConsultantAi with { Roles = [AiRole.Reviewer] }, ConsultantAi with { Roles = [AiRole.Executive] }, ConsultantAi with { Roles = [AiRole.Support] }, ConsultantAi with { Actions = default }, ConsultantAi with { Actions = [] }, ConsultantAi with { Actions = [AiAction.Override] }, ConsultantAi with { Actions = [(AiAction)999] }, ConsultantAi with { Categories = default }, ConsultantAi with { Categories = [] }, ConsultantAi with { Categories = ["SECURITY"] }, ConsultantAi with { Categories = ["unknown"] }, ConsultantAi with { Active = false }, ConsultantAi with { Authenticated = false }, ConsultantAi with { AssignmentActive = false }, ConsultantAi with { Revoked = true }, ConsultantAi with { AiPolicyAllowed = false }, ConsultantAi with { ResourceState = AiResourceState.Published }, ConsultantAi with { ResourceState = AiResourceState.Deleted }, ConsultantAi with { ResourceState = AiResourceState.Cancelled }];
        foreach (var authority in bad) OwnerDenied(await OwnerRead(run.RunId, digest, authority), AiIssue.Denied, "closed current owning authority denial");
        OwnerDenied(await OwnerRead(run.RunId, digest, ConsultantAi with { Scope = AiScope.Fixed with { ProjectId = "synthetic-other" } }), AiIssue.WrongScope, "owning wrong scope remains exact issue");
        Check(before == await Rows(), "all direct owner authority reads preserve whole-table bytes");
    }
    private static async Task DirectInputMatrix(SyntheticRunSnapshot run)
    {
        var digest = await SourceDigest(run.RunId);
        await using var c = await Open(); await using var other = await Open(); await using var t = await c.BeginTransactionAsync();
        foreach (var malformed in new[] { "", new string('A', 64), new string('0', 63), "foreign" })
            OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, malformed), AiIssue.InvalidInput, "strict owning expected digest admission");
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, Guid.Empty, digest), AiIssue.InvalidInput, "empty owning UUID refused");
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(other, t, ConsultantAi, run.RunId, digest), AiIssue.InvalidInput, "foreign owning transaction refused");
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, new string('0', 64)), AiIssue.SourceConflict, "stale source snapshot digest not replaced");
        var foreign = new NpgsqlConnectionStringBuilder(Database) { Database = Prefix + "uncreated" };
        var configured = new SyntheticAiExecutionStore(foreign.ConnectionString);
        OwnerDenied(await configured.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, digest), AiIssue.WrongScope, "owning configured DB mismatch no alternate connection");
        await using var closed = new NpgsqlConnection(Database);
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(closed, t, ConsultantAi, run.RunId, digest), AiIssue.InvalidInput, "owning closed connection refused");
        await t.RollbackAsync();
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, digest), AiIssue.InvalidInput, "owning rolled-back transaction refused");
        await t.DisposeAsync();
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, digest), AiIssue.InvalidInput, "owning disposed transaction refused");
        await using var commit = await c.BeginTransactionAsync(); await commit.CommitAsync();
        OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, commit, ConsultantAi, run.RunId, digest), AiIssue.InvalidInput, "owning committed transaction refused");
    }
    private static JsonObject ExpectedProof(AiExecutionSnapshot source, FixtureExpected fixture)
    {
        var works = new JsonArray();
        foreach (var work in source.Works.OrderBy(w => w.Work.WorkId, StringComparer.Ordinal))
        {
            var packet = JsonNode.Parse(work.Work.PacketInputJson)!;
            var last = work.Attempts[^1]; JsonObject? accepted = null;
            if (fixture.AcceptedCanonical is not null)
                accepted = new() { ["attempt"] = JsonNode.Parse(AiExecutionCanonical.Serialize(last.Key)), ["receiptId"] = fixture.ReceiptId, ["outputDigest"] = fixture.OutputDigest, ["outputSchemaVersion"] = "synthetic-ai-fixture-output-v1", ["acceptedSnapshotDigest"] = Hash(fixture.AcceptedCanonical), ["acceptedSnapshotSchemaVersion"] = "synthetic-ai-proposal-snapshot-v1" };
            works.Add(new JsonObject { ["workId"] = work.Work.WorkId, ["category"] = work.Work.Category, ["workRevision"] = work.Revision, ["state"] = work.State.ToString(), ["inputSchemaVersion"] = packet["schemaVersion"]!.GetValue<string>(), ["packetDigest"] = last.Key.PacketDigest, ["accepted"] = accepted, ["missingReason"] = accepted is null ? "NoAcceptedOutput" : null });
        }
        return new() { ["schemaVersion"] = "synthetic-ai-accepted-schema-proof-v1", ["runLock"] = JsonNode.Parse(AiExecutionCanonical.Serialize(source.RunLock)), ["sourceSnapshotDigest"] = source.ContentDigest, ["works"] = works };
    }
    private static string SchemaBundle(JsonElement proof)
    {
        var map = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal) { ["Q.$.sourceSnapshotDigest"] = proof.GetProperty("sourceSnapshotDigest") };
        var index = 0;
        foreach (var work in proof.GetProperty("works").EnumerateArray())
        {
            var p = $"Q.$.works[{index++}].";
            foreach (var key in new[] { "workId", "category", "workRevision", "state", "inputSchemaVersion", "packetDigest", "missingReason" }) map[p + key] = work.GetProperty(key);
            if (work.GetProperty("accepted").ValueKind == JsonValueKind.Null) continue;
            var accepted = work.GetProperty("accepted");
            foreach (var field in accepted.GetProperty("attempt").EnumerateObject()) map[p + "accepted.attempt." + field.Name] = field.Value;
            foreach (var key in new[] { "receiptId", "outputDigest", "outputSchemaVersion", "acceptedSnapshotDigest", "acceptedSnapshotSchemaVersion" }) map[p + "accepted." + key] = accepted.GetProperty(key);
        }
        return Canonical(JsonSerializer.SerializeToElement(map));
    }
    private static async Task VerifyReadiness(Phase1BAcceptedSchemaReadinessProjection readiness, SyntheticRunSnapshot run, NpgsqlConnection c, NpgsqlTransaction t)
    {
        var fixture = Fixtures[run.RunId];
        var oldRead = await Ai.ReadInTransactionAsync(c, t, ConsultantAi, run.RunId);
        Check(oldRead.Succeeded && oldRead.Value is not null, "same-transaction unchanged owning AI read");
        var source = oldRead.Value!;
        Check(source.Works.Single().Attempts.All(a => a.Receipt is null || a.Receipt.OutputJson is null), "old owner output remains stripped");
        var expectedNode = ExpectedProof(source, fixture); var expected = Canonical(JsonSerializer.SerializeToElement(expectedNode));
        Check(readiness.SchemaProofCanonicalJson == expected && readiness.SchemaProofContentDigest == Hash(expected), "complete public schema proof equals independently assembled owning-source canonical/hash");
        using var proof = JsonDocument.Parse(expected);
        var direct = await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, source.ContentDigest);
        Check(direct.Value is not null && direct.Value.CanonicalJson == expected && direct.Value.ContentDigest == Hash(expected), "direct owning proof equals composed exact native snapshot");
        Check(direct.Value!.Works.Count == 1 && (direct.Value.Works.Single().Accepted is not null) == (fixture.AcceptedCanonical is not null), "one work proof independent of finding cardinality");
        Check(direct.Value.GetType().GetProperties().All(p => p.SetMethod is null) && ((IList)direct.Value.Works).IsReadOnly, "owning proof detached/get-only surface");
        Check(direct.Value.Works.Single().Accepted?.Attempt.Ordinal == (fixture.AcceptedCanonical is null ? null : fixture.Attempts), "last accepted ordinal only, retry history not duplicated");
        using var oldPopulation = JsonDocument.Parse(readiness.PopulationCanonicalJson); var pop = oldPopulation.RootElement;
        Check(Hash(readiness.PopulationCanonicalJson) == readiness.PopulationContentDigest, "lossless old population bytes/hash");
        Check(pop.GetProperty("missingVersionKinds").GetArrayLength() == 10 && !pop.GetProperty("completeSamplingReady").GetBoolean(), "old population retains13/10 false readiness");
        Check(pop.GetProperty("members").GetArrayLength() == fixture.Proposals && pop.GetProperty("gaps").GetArrayLength() == 2 - fixture.Proposals, "source finding and gap counts remain literal");
        using var captured = JsonDocument.Parse(pop.GetProperty("sourceCanonicalJson").GetString()!); var capture = captured.RootElement;
        using var emittedAi = JsonDocument.Parse(capture.GetProperty("aiSnapshotJson").GetString()!);
        Check(capture.GetProperty("aiSnapshotDigest").GetString() == source.ContentDigest && emittedAi.RootElement.GetProperty("contentDigest").GetString() == source.ContentDigest, "C/A/proof current snapshot digest equality");
        Check(readiness.RunId == run.RunId && readiness.RunRevision == run.Revision && readiness.RunRevision == pop.GetProperty("runRevision").GetInt64(), "assessment revision remains actual run revision");
        var fields = JsonNode.Parse(pop.GetProperty("versionBindings").GetRawText())!.AsArray();
        var binding = readiness.VersionBindings.Single(b => b.Kind == SamplingVersionKind.AiSchema);
        var oldSchema = fields.Single(b => b!["kind"]!.GetValue<string>() == "AiSchema")!;
        if (fixture.AcceptedCanonical is not null)
        {
            var value = SchemaBundle(proof.RootElement); using var bundle = JsonDocument.Parse(value);
            Check(bundle.RootElement.EnumerateObject().Count() == 17, "actual successful Q bundle has fixed17 native keys");
            Check(binding.ValueJson == value && binding.Reference == Reference("version-binding", "AiSchema", value), "actual AiSchema full native bundle and independently framed alias");
            Check(binding.OriginPaths.SequenceEqual(bundle.RootElement.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal)), "actual Q origin paths exact sorted concrete keys");
            Check(binding.KnownFactsJson == oldSchema["knownFactsJson"]!.GetValue<string>(), "old partial schema facts retained byte-identical independently");
            oldSchema["state"] = "SourceBound"; oldSchema["reference"] = binding.Reference; oldSchema["valueJson"] = value; oldSchema["originPaths"] = JsonSerializer.SerializeToNode(binding.OriginPaths); oldSchema["missingReason"] = null;
            Check(readiness.MissingVersionKinds.Count == 9, "exact nine remaining metadata gaps");
        }
        else
        {
            Check(binding.ValueJson is null && binding.Reference is null && binding.MissingReason == "AcceptedOutputSchemaNotSupplied", "terminal noaccepted preserves missing schema without placeholders");
            Check(readiness.MissingVersionKinds.Count == 10 && direct.Value.Works.Single().Accepted is null && direct.Value.Works.Single().MissingReason == "NoAcceptedOutput", "zero accepted records stays13/10 explicit absence");
        }
        foreach (var native in fields)
        {
            var kind = Enum.Parse<SamplingVersionKind>(native!["kind"]!.GetValue<string>()); var actual = readiness.VersionBindings.Single(b => b.Kind == kind);
            var actualNode = new JsonObject { ["kind"] = actual.Kind.ToString(), ["state"] = actual.State.ToString(), ["reference"] = actual.Reference, ["valueJson"] = actual.ValueJson, ["knownFactsJson"] = actual.KnownFactsJson, ["missingReason"] = actual.MissingReason, ["originPaths"] = JsonSerializer.SerializeToNode(actual.OriginPaths) };
            Check(Canonical(JsonSerializer.SerializeToElement(actualNode)) == Canonical(JsonSerializer.SerializeToElement(native)), "exact untouched22 bindings or sole schema replacement " + kind);
        }
        var expectedReadiness = new JsonObject { ["schemaVersion"] = "synthetic-phase1b-evaluation-schema-readiness-v1", ["runId"] = run.RunId.ToString("D"), ["runRevision"] = run.Revision, ["scopeId"] = Reference("scope"), ["environmentId"] = Reference("environment", "synthetic-environment"), ["populationCanonicalJson"] = readiness.PopulationCanonicalJson, ["populationContentDigest"] = Hash(readiness.PopulationCanonicalJson), ["schemaProofCanonicalJson"] = expected, ["schemaProofContentDigest"] = Hash(expected), ["versionBindings"] = fields, ["missingVersionKinds"] = JsonSerializer.SerializeToNode(fields.Where(b => b!["state"]!.GetValue<string>() == "Missing").Select(b => b!["kind"]!.GetValue<string>()).ToArray()), ["completeSamplingReady"] = false };
        var canonical = Canonical(JsonSerializer.SerializeToElement(expectedReadiness));
        Check(readiness.CanonicalJson == canonical && readiness.ContentDigest == Hash(canonical), "full new readiness canonical fields/hash independently assembled");
        Check(!readiness.CompleteSamplingReady && readiness.VersionBindings.Count == 23 && readiness.VersionBindings.Select(b => b.Kind).SequenceEqual(Enum.GetValues<SamplingVersionKind>()), "exact23 declaration order and incomplete sampling readiness");
        foreach (var member in capture.GetProperty("members").EnumerateArray()) foreach (var occurrence in member.GetProperty("occurrences").EnumerateArray()) { using var original = JsonDocument.Parse(occurrence.GetProperty("originalJson").GetString()!); Expected.Original(original.RootElement); }
    }
    private static async Task NoAccepted()
    {
        var run = await Finished(-1); var before = await Rows();
        await using var c = await Open(); await using var t = await c.BeginTransactionAsync();
        var result = await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome);
        Check(result.HasReadiness, "actual rejected terminal zeroaccepted source captured");
        await VerifyReadiness(result.Readiness!, run, c, t); await t.CommitAsync();
        Check(before == await Rows(), "zeroaccepted caller COMMIT nonmutation");
    }
    private static async Task RetrySource()
    {
        var run = await Finished(2, retry: true); var before = await Rows();
        await using var c = await Open(); await using var t = await c.BeginTransactionAsync();
        var result = await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome);
        Check(result.HasReadiness, "actual two retries then accepted final source");
        await VerifyReadiness(result.Readiness!, run, c, t); await t.CommitAsync();
        Check(before == await Rows(), "retry success caller COMMIT nonmutation");
    }
    private static async Task AcceptedTamper(SyntheticRunSnapshot run)
    {
        var digest = await SourceDigest(run.RunId);
        foreach (var sql in new[] { "UPDATE synthetic_ai_execution.accepted_snapshot SET source_json='{}' WHERE attempt_id=@attempt", "UPDATE synthetic_ai_execution.accepted_snapshot SET canonical='{}' WHERE attempt_id=@attempt", "UPDATE synthetic_ai_execution.accepted_snapshot SET digest=repeat('0',64) WHERE attempt_id=@attempt", "DELETE FROM synthetic_ai_execution.accepted_snapshot WHERE attempt_id=@attempt", "UPDATE synthetic_ai_execution.accepted_snapshot SET source_json=jsonb_set(source_json::jsonb,'{schemaVersion}','null')::text WHERE attempt_id=@attempt", "INSERT INTO synthetic_ai_execution.accepted_snapshot SELECT '77777777-7777-4777-8777-777777777777'::uuid,canonical,digest,source_json FROM synthetic_ai_execution.accepted_snapshot WHERE attempt_id=@attempt" })
        {
            var before = await Rows(); await using var c = await Open(); await using var t = await c.BeginTransactionAsync();
            await using (var q = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.accepted_snapshot DISABLE TRIGGER USER; " + sql + "; ALTER TABLE synthetic_ai_execution.accepted_snapshot ENABLE TRIGGER USER", c, t))
            { q.Parameters.AddWithValue("attempt", Guid.Parse("b50aa02f-6e30-4f64-af00-b0ba1042a7fa")); await q.ExecuteNonQueryAsync(); }
            OwnerDenied(await Ai.ReadAcceptedSchemaInTransactionAsync(c, t, ConsultantAi, run.RunId, digest), AiIssue.IntegrityMismatch, "private accepted schema/raw/canonical/missing/orphan corruption denied");
            Denied(await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BAcceptedSchemaIssue.IntegrityMismatch, "composition fails closed on native accepted corruption");
            await t.RollbackAsync(); Check(before == await Rows(), "accepted tamper rollback retains exact native rows");
        }
    }
}

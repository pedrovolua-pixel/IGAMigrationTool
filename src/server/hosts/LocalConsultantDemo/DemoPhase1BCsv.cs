using System.Collections.Immutable;
using System.Text.Json;
using AssessmentRuns;
using FindingReview;
using Npgsql;
using SyntheticAiExecution;
using SyntheticFixReview;
using SyntheticOutcomePriority;
using SyntheticPlanningTasks;
using SyntheticTaskCsv;
using SyntheticTaskCsvIntegration;

internal sealed partial class DemoPhase1BService
{
    private CsvAuditStore Audit => new(connection);
    internal async Task InitializeCsv(CancellationToken ct = default) => await Audit.InitializeAsync(ct);
    internal PlanningTaskExportAuthority ExportAuthority => new(Auditor ? "synthetic-auditor" : "synthetic-consultant", true, true, false, true,
        PlanningTaskScope.Fixed, Auditor ? PlanningTaskExportRole.Auditor : PlanningTaskExportRole.Consultant, ["SECURITY", "OPERATIONS"], true, true, Auditor, true);
    private async Task<CsvEnvelope> ExportCapture(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, CancellationToken ct)
    {
        var authority = ExportAuthority;
        Require(PlanningTaskExportPolicy.Authorize(authority, PlanningTaskScope.Fixed));
        var read = await engine.ReadInTransactionAsync(c, t, DemoFixtureCatalog.Scope, runId, ct); Require(read.Issue);
        var run = read.Snapshot!;
        if (!DemoPhase1BCatalog.MatchesFrozenFixture(run)) throw new Phase1BDeniedException("SourceConflict");
        var outcome = await Outcomes.VerifyLockedForExportInTransactionAsync(c, t, runId,
            new(authority.ActorId, true, true, false, true, OutcomeScope.Fixed, Auditor ? OutcomeRole.Auditor : OutcomeRole.Consultant,
                authority.Categories, OutcomeResourceState.Mutable, true, true, Auditor), ct); Require(outcome.Issue);
        if (outcome.Proof!.OutcomeLockDigest != run.FrozenInputs.Phase1BLocks!.OutcomeLockDigest || outcome.Proof.ContractDigest != DemoPhase1BCatalog.OutcomeContractDigest)
            throw new Phase1BDeniedException("IntegrityMismatch");
        var ai = await Ai.VerifyForExportInTransactionAsync(c, t,
            new(authority.ActorId, AiScope.Fixed, [Auditor ? AiRole.Auditor : AiRole.Consultant], authority.Categories, true, true, true, false, true, true, Auditor, AiResourceState.Mutable), runId, ct); Require(ai.Issue);
        var originals = SyntheticPhase1BAnalysisAdapter.OriginalsForExport(run, ai.Value!) ?? throw new Phase1BDeniedException("IntegrityMismatch");
        var review = await reviews.ReadForExportInTransactionAsync(c, t, runId,
            new(authority.ActorId, SyntheticReviewScope.Fixed, Auditor ? SyntheticReviewExportRole.Auditor : SyntheticReviewExportRole.Consultant,
                authority.Categories, true, true, true, false, true, true, Auditor, true), ct); Require(review.Issue);
        if (SyntheticReviewDigest.Compute(review.Capture!.Snapshot.RunSeed) != SyntheticReviewDigest.Compute(Seed(run, originals)))
            throw new Phase1BDeniedException("IntegrityMismatch");
        var context = new DemoReviewContext(null, review.Capture.Snapshot);
        var guidance = DemoRecommendationGuidanceProjection.FromVerifiedAnalysis(run, originals, context);
        var packages = DemoFixPackageProjection.Detail(run, guidance, context, originals);
        var artifactSource = ArtifactReviewSourceBuilder.Build(packages?.Snapshot); Require(artifactSource.Issue);
        var artifact = await artifacts.ReadForExportInTransactionAsync(c, t, runId,
            new(authority.ActorId, true, true, false, true, ArtifactReviewScope.Fixed, Auditor ? ArtifactExportRole.Auditor : ArtifactExportRole.Consultant,
                authority.Categories, true, true, Auditor, true), artifactSource.Source!, ct); Require(artifact.Issue);
        var source = PlanningTaskSourceBuilder.Build(packages!.Snapshot, artifact.Capture!.Snapshot); Require(source.Issue);
        var capture = await Tasks.CaptureForExportInTransactionAsync(c, t, runId, authority,
            (_, _, _, _, _) => Task.FromResult(source), ct); Require(capture.Issue);
        var result = CsvExportCaptureAdapter.CreateEnvelope(capture.Capture!, VersionLocks(run)); Require(result.Issue); return result.Envelope!;
    }
    private static CsvVersionLocks VersionLocks(SyntheticRunSnapshot run)
    {
        var v = run.FrozenInputs; var l = v.Phase1BLocks!;
        if (JsonSerializer.Serialize(l) != JsonSerializer.Serialize(DemoPhase1BAiFixture.Locks(run.RunId, l.OutcomeLockDigest))) throw new Phase1BDeniedException("IntegrityMismatch");
        return new(run.BaselineCatalogId, run.ProfileCatalogId, run.InputDigest,
            new(v.ProfileVersion, v.DesiredOutcomeVersion, v.ScoringAlgorithmVersion, v.AiPolicyVersion, v.PromptVersion, v.ModelVersion,
                v.ApplicationVersion, v.WorkSchemaVersion, v.ScriptedResultsDigest, v.AnalysisFixtureDigest!, v.MaturityFixtureDigest!,
                v.FixPackageTemplateDigest!, v.FixReviewContractDigest!, v.PlanningTaskContractDigest!,
                new(l.SchemaVersion, l.OutcomeContractDigest, l.PriorityPolicyVersion, l.AiContractDigest, l.CsvContractDigest, l.OutcomeLockDigest,
                    l.AiFixtureDigest, l.AiMappingDigest, l.AiPacketDigest, l.FixtureEpoch)));
    }
    internal Task<CsvEnvelope> Inspect(Guid runId, CancellationToken ct) => Fenced(runId, (c, t) => ExportCapture(c, t, runId, ct), ct);
    internal Task<object> Navigate(Guid runId, string view, string id, CancellationToken ct) => Fenced(runId, async (c, t) =>
    {
        if (view is not ("tasks" or "findings") || !CsvCodec.ValidDigest(id)) throw new Phase1BDeniedException("Denied");
        var capture = await ExportCapture(c, t, runId, ct);
        if (view == "tasks")
        { var row = capture.Rows.SingleOrDefault(r => r.TaskId == id) ?? throw new Phase1BDeniedException("Denied"); return (object)new { schemaVersion = 1, demoOnly = true, view, runId, task = row, readOnly = Auditor }; }
        var matching = capture.Rows.Where(r => r.FindingId == id).ToArray();
        if (matching.Length == 0) throw new Phase1BDeniedException("Denied");
        return new { schemaVersion = 1, demoOnly = true, view, runId, findingId = id, tasks = matching.Select(r => r.TaskId).ToArray(), readOnly = Auditor };
    }, ct);
    internal async Task<IResult> Export(Guid runId, Guid requestId, string inspectedDigest, string rendererDirectory, CancellationToken ct)
    {
        if (requestId == Guid.Empty || !CsvCodec.ValidDigest(inspectedDigest)) throw new Phase1BDeniedException("InvalidInput");
        CsvEnvelope? captured = null; byte[]? output = null;
        try
        {
            captured = await Fenced(runId, async (c, t) =>
            {
                var envelope = await ExportCapture(c, t, runId, ct);
                if (envelope.SnapshotDigest != inspectedDigest) throw new Phase1BDeniedException("SourceConflict");
                await AuditEvent(c, t, requestId, runId, CsvAuditStage.Request, envelope, null, ct);
                await AuditEvent(c, t, requestId, runId, CsvAuditStage.Dispatch, envelope, null, ct); return envelope;
            }, ct);
            var rendered = await new CsvSandboxRunner(rendererDirectory).RenderAsync(captured, ct); Require(rendered.Issue); output = rendered.Bytes!;
            var verified = CsvCodec.Verify(captured, output, requestId); Require(verified.Issue);
            return new DisposableCsvResult(output, async (context, bytes) =>
            {
                // Session locks use the identical ordered source keys and survive the final audit commit.
                // The nonpooled connection closes on every path, releasing both locks even on a broken client.
                var deliveryCommitted = false;
                try
                {
                    var deliveryConnection = new NpgsqlConnectionStringBuilder(connection) { Pooling = false };
                    await using var c = new NpgsqlConnection(deliveryConnection.ConnectionString); await c.OpenAsync(context.RequestAborted);
                    await SyntheticSourceFence.SyntheticRunSourceFence.AcquireSessionAsync(c, "synthetic-customer", "synthetic-project", "synthetic-environment", DemoPhase1BCatalog.RegistryFenceId, context.RequestAborted);
                    await SyntheticSourceFence.SyntheticRunSourceFence.AcquireSessionAsync(c, "synthetic-customer", "synthetic-project", "synthetic-environment", runId, context.RequestAborted);
                    await using (var t = await c.BeginTransactionAsync(context.RequestAborted))
                    {
                        var current = await ExportCapture(c, t, runId, context.RequestAborted);
                        Require(CsvExportCaptureAdapter.Recheck(captured, current, ExportAuthority));
                        await AuditEvent(c, t, requestId, runId, CsvAuditStage.Render, captured, verified.Receipt!.OutputSha256, context.RequestAborted);
                        await AuditEvent(c, t, requestId, runId, CsvAuditStage.Delivery, captured, verified.Receipt.OutputSha256, context.RequestAborted);
                        await t.CommitAsync(context.RequestAborted); deliveryCommitted = true;
                    }
                    context.Response.ContentType = "text/csv; charset=utf-8"; context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.ContentDisposition = "attachment; filename=synthetic-phase1b-tasks.csv";
                    context.Response.ContentLength = bytes.Length;
                    await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
                }
                catch
                {
                    using var auditDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    if (!deliveryCommitted && !context.Response.HasStarted) await Denial(requestId, runId, captured, auditDeadline.Token);
                    else if (deliveryCommitted)
                    {
                        try
                        {
                            await Fenced(runId, async (c, t) =>
                        {
                            var interrupted = new CsvAuditEvent(Guid.NewGuid(), requestId, runId, ExportAuthority.ActorId, CsvScope.Fixed, DateTimeOffset.UtcNow,
                                CsvAuditStage.Denial, CsvAuditReason.TransferInterrupted, captured.SnapshotDigest, verified.Receipt!.OutputSha256, captured.Rows.Length);
                            Require(await Audit.AppendInTransactionAsync(c, t, interrupted, auditDeadline.Token)); return true;
                        }, auditDeadline.Token);
                        }
                        catch (Exception) { /* Metadata interruption failure cannot recall prior bytes. */ }
                    }
                    throw;
                }
            });
        }
        catch
        {
            if (output is not null) Array.Clear(output);
            await Denial(requestId, runId, captured, ct);
            throw;
        }
    }
    private async Task Denial(Guid requestId, Guid runId, CsvEnvelope? captured, CancellationToken ct)
    {
        try
        {
            await Fenced(runId, async (c, t) =>
            {
                var denial = new CsvAuditEvent(Guid.NewGuid(), requestId, runId, ExportAuthority.ActorId, CsvScope.Fixed, DateTimeOffset.UtcNow,
                    CsvAuditStage.Denial, CsvAuditReason.Denied, captured?.SnapshotDigest, null, captured?.Rows.Length);
                Require(await Audit.AppendInTransactionAsync(c, t, denial, ct)); return true;
            }, ct);
        }
        catch (Exception) { /* Unavailable audit still denies every output byte. */ }
    }
    private async Task AuditEvent(NpgsqlConnection c, NpgsqlTransaction t, Guid requestId, Guid runId, CsvAuditStage stage, CsvEnvelope envelope, string? output, CancellationToken ct)
    {
        var audit = new CsvAuditEvent(Guid.NewGuid(), requestId, runId, ExportAuthority.ActorId, CsvScope.Fixed, DateTimeOffset.UtcNow, stage,
            CsvAuditReason.None, envelope.SnapshotDigest, output, envelope.Rows.Length);
        Require(await Audit.AppendInTransactionAsync(c, t, audit, ct));
    }
}
internal sealed class DisposableCsvResult(byte[] bytes, Func<HttpContext, byte[], Task> deliver) : IResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        try { await deliver(context, bytes); }
        finally { Array.Clear(bytes); }
    }
}

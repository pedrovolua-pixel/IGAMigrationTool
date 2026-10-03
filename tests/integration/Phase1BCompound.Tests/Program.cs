using System.Collections.Immutable;
using System.Reflection;
using AssessmentCoverage;
using AssessmentRuns;
using FindingReview;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SyntheticAiExecution;
using SyntheticFixReview;
using SyntheticOutcomePriority;
using SyntheticPlanningTasks;
using SyntheticTaskCsv;

internal static partial class Program
{
    internal const string Connection = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_phase1b_compound_tests;Username=iga_synthetic";
    internal static readonly CancellationToken Token = CancellationToken.None;
    internal static int Checks;
    internal static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); Checks++; Console.WriteLine("PASS " + label); }
    private static async Task Main()
    {
        await CreateOwnedDatabase();
        await ResetOwnedFixture();
        var observer = new InjectedObserver();
        var engine = new SyntheticDurableRunEngine(Connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromSeconds(8), 3, 512), observer);
        var reviews = new SyntheticReviewStore(Connection, SyntheticReviewScope.Fixed);
        var service = new DemoPhase1BService(Connection, engine, reviews, false);
        await engine.InitializeAsync(); await reviews.InitializeAsync(); await service.InitializeAsync(); await service.InitializeCsv();
        var selected = await ApproveOutcome(service);
        await StartRollback(service, engine, observer, selected);
        await LateStartRollback(service, engine, selected);
        await AiRollback(service, engine, observer, selected, false);
        await AiRollback(service, engine, observer, selected, true);
        await StaleLease(service, engine, observer, selected);
        var run = await Finish(service, engine, selected);
        await CaptureAndCsv(service, engine, reviews, run);
        Console.WriteLine($"PASS {Checks} compound integration assertions; actual owning PostgreSQL and coordinator host APIs.");
    }
    private static async Task CreateOwnedDatabase()
    {
        await using var c = new NpgsqlConnection("Host=127.0.0.1;Port=55433;Database=postgres;Username=iga_synthetic"); await c.OpenAsync();
        await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname='iga_synthetic_phase1b_compound_tests'", c);
        if ((long)(await exists.ExecuteScalarAsync())! == 0)
        { await using var create = new NpgsqlCommand("CREATE DATABASE iga_synthetic_phase1b_compound_tests", c); await create.ExecuteNonQueryAsync(); }
    }
    internal static async Task<long> Scalar(string sql, Guid run)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var q = new NpgsqlCommand(sql, c); q.Parameters.AddWithValue("run", run);
        return (long)(await q.ExecuteScalarAsync())!;
    }
    private static async Task<ImmutableArray<OutcomeSelection>> ApproveOutcome(DemoPhase1BService service)
    {
        const string id = "compound-access-governance";
        var registry = await service.Registry(Token);
        var approved = registry.Entries.SingleOrDefault(e => e.Content.OutcomeId == id && e.State == OutcomeState.CustomerApproved);
        if (approved is not null) return [new(id, approved.Content.Version, approved.Content.ContentDigest, approved.Revision, approved.History[^1].EventId)];
        var content = OutcomePriorityCanonical.Seal(new OutcomeContentVersion(id, 1, "SECURITY", "Fictional compound objective", "Fictional controls remain traceable",
            OutcomeOrigin.Documented, [DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys.First()], ["synthetic-reference"], [], null, ""));
        var created = await service.Outcome(new(Guid.NewGuid(), OutcomeKind.CreateDraft, id, 1, 0, content.ContentDigest, registry.Revision, null, content, "Fictional compound test draft"), Token);
        Check(created.Receipt is not null, "real outcome draft recorded");
        registry = await service.Registry(Token);
        var reviewed = await service.Outcome(new(Guid.NewGuid(), OutcomeKind.Review, id, 1, 1, content.ContentDigest, registry.Revision, null, null, "Fictional Consultant review"), Token);
        registry = await service.Registry(Token);
        var approval = Guid.NewGuid();
        var accepted = await service.Outcome(new(approval, OutcomeKind.Approve, id, 1, 2, content.ContentDigest, registry.Revision, reviewed.Receipt!.EventId, null, "Fictional customer approval"), Token);
        Check(accepted.Receipt!.ActorId == "synthetic-customer-outcome-approver-v1", "actual distinct customer approval authority used");
        return [new(id, 1, content.ContentDigest, accepted.Receipt.Revision, approval)];
    }
    internal static async Task<SyntheticRunSnapshot> Current(SyntheticDurableRunEngine engine, Guid run)
    { var read = await engine.ReadAsync(DemoFixtureCatalog.Scope, run); Check(read.Succeeded, "actual owning run read succeeds"); return read.Snapshot!; }
    internal static Task<AiExecutionSnapshot> Ai(DemoPhase1BService service, Guid run) => service.Fenced(run, async (c, t) =>
    { var read = await service.Ai.ReadInTransactionAsync(c, t, DemoPhase1BService.Worker, run); Check(read.Succeeded, "actual owning AI proof read succeeds"); return read.Value!; }, Token);
    internal static async Task<(SyntheticRunSnapshot Run, Guid Generation, AiRecovery Recovery)> Dispatch(DemoPhase1BService service, SyntheticDurableRunEngine engine, ImmutableArray<OutcomeSelection> selected)
    {
        var run = (await service.Start(Guid.NewGuid(), selected, Token)).Snapshot!;
        var lease = await engine.AcquireLeaseAsync(run.Scope, run.RunId, "synthetic-compound-test-worker"); Check(lease.Succeeded, "real run lease acquired");
        run = lease.Snapshot!; var generation = run.Lease!.Generation;
        var deterministic = DemoPhase1BCatalog.DeterministicPlan.ExpectedResults.ToArray();
        var begun = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, deterministic.Select(x => x.Key).ToArray()); Check(begun.Succeeded, "real deterministic work begun");
        var saved = await engine.CheckpointAsync(run.Scope, run.RunId, generation, begun.Snapshot!.Revision, deterministic); Check(saved.Succeeded, "real deterministic coverage saved"); run = saved.Snapshot!;
        var work = (await Ai(service, run.RunId)).Works.Single();
        var reserved = await service.Fenced(run.RunId, (c, t) => service.Ai.ReserveAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, Guid.NewGuid(), work.Revision), Token);
        Check(reserved.Succeeded, "actual AI reservation committed before dispatch");
        var dispatched = await service.Fenced(run.RunId, (c, t) => service.Ai.MarkDispatchedAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, reserved.Value!.Key, reserved.Value.WorkRevision), Token);
        Check(dispatched.Succeeded, "actual dispatch journal committed before pure provider");
        return (run, generation, new(dispatched.Value!.Key, work.Work.Scenario, work.Work.PacketInputJson, work.Work.Units, false));
    }
    internal static Task Commit(DemoPhase1BService service, SyntheticDurableRunEngine engine, SyntheticRunSnapshot run, Guid generation, AiRecovery recovery, AiProviderReceipt receipt)
    {
        var worker = new DemoPhase1BWorker(service, engine, NullLogger<DemoPhase1BWorker>.Instance);
        var method = typeof(DemoPhase1BWorker).GetMethod("CommitReceipt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(worker, [run, generation, DemoPhase1BAiFixture.Works(run.RunId).Single().WorkId, recovery, receipt, Token])!;
    }
    internal static async Task ExpectDenied(Func<Task> call, string label)
    {
        try { await call(); throw new InvalidOperationException("expected denial: " + label); }
        catch (Phase1BDeniedException) { Check(true, label); }
        catch (InjectedFailure) { Check(true, label); }
    }
    internal static async Task<SyntheticRunSnapshot> Finish(DemoPhase1BService service, SyntheticDurableRunEngine engine, ImmutableArray<OutcomeSelection> selected)
    {
        var fixture = await Dispatch(service, engine, selected);
        // The pure provider is called after the owning dispatch transaction has committed.
        var receipt = new FakeAiProvider().Dispatch(fixture.Recovery);
        await Commit(service, engine, fixture.Run, fixture.Generation, fixture.Recovery, receipt);
        var current = await Current(engine, fixture.Run.RunId);
        var complete = await engine.CompleteCoverageAsync(current.Scope, current.RunId, fixture.Generation, current.Revision); Check(complete.Succeeded, "actual full union coverage reaches Scoring");
        return complete.Snapshot!;
    }
}
internal sealed class InjectedFailure : Exception;
internal sealed class InjectedObserver : ISyntheticRunCommitObserver
{
    internal Guid Target;
    internal string? Operation;
    internal bool Expire;
    public async Task BeforeCommitAsync(string operation, Guid runId, CancellationToken ct)
    {
        if (runId != Target || operation != Operation) return;
        Operation = null;
        if (Expire) await Task.Delay(TimeSpan.FromSeconds(9), ct); else throw new InjectedFailure();
    }
}

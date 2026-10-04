using System.Security.Cryptography;
using CollectorHost;
using CollectorSafety;

internal static class RunCoordinatorChecks
{
    public static async Task<int> RunAsync(CollectorConfig config)
    {
        var count = 0;
        var directory = Path.Combine(Path.GetTempPath(), "iga-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows()) WindowsStageTestDirectory.Protect(directory);
        try
        {
            var configPath = Path.Combine(directory, "collector.json");
            var runId = Guid.NewGuid();
            var stageDirectory = OperatingSystem.IsWindows()
                ? WindowsRunDirectoryProvisioner.ProvisionNew(directory, runId)
                : Directory.CreateDirectory(Path.Combine(directory, runId.ToString("N"))).FullName;
            var key = RandomNumberGenerator.GetBytes(32);
            var included = new FieldKey("schema", "uid");
            var excluded = new FieldKey("schema", "display");
            var approval = new ApprovedCollectorRun(
                config.QueryPackId, config.QueryPackVersion, config.QueryPackSha256,
                new SourceBuildClaim(config.ExactBuild, []),
                new QueryApplicabilityRule("schema-inventory", [config.ExactBuild], null, []),
                new PermissionProbe(true, [SourceCapability.MinimumRead]),
                new FieldPolicySnapshot(config.FieldPolicyId.ToString("D"),
                    config.FieldPolicyVersion.ToString(), config.FieldPolicySha256,
                    new HashSet<FieldKey> { included }, new HashSet<string>()),
                "uid", 10, 100, TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow,
                Path.Combine(stageDirectory, "run-start.igr"),
                Path.Combine(stageDirectory, "checkpoint.enc"), key);
            var first = new CollectorPage("page-1", 1,
                [new FieldCandidate(included, FieldClassification.ApprovedReference, "uid-1"),
                    new FieldCandidate(excluded, FieldClassification.ApprovedReference, "private-display")], true);
            var second = new CollectorPage("page-2", 1,
                [new FieldCandidate(included, FieldClassification.ApprovedReference, "uid-2")], false);
            var stageContext = new PageCheckpointContext(approval.Query.QueryId,
                $"{approval.QueryPackId:D}/{approval.QueryPackVersion}/{approval.QueryPackSha256}",
                config.ExactBuild, config.ScopeId.ToString("D"),
                $"{config.FieldPolicyId:D}/{config.FieldPolicyVersion}/{config.FieldPolicySha256}",
                approval.OrderingKey);

            var fake = new FakeAdapter(approval, [first, second], stageDirectory, stageContext,
                config.MaxLocalBytes, key);
            Check("disabled has no adapter calls",
                (await new CollectorRunCoordinator(fake).RunAsync(configPath,
                    config with { Enabled = false }, CancellationToken.None)).Outcome == CollectorRunOutcome.Disabled &&
                fake.Loads == 0);

            fake.Approval = null;
            Check("missing approved material stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.SourceContractPending && fake.Reads == 0);
            fake.CancelApprovalWithoutToken = true;
            Check("unexpected approval cancellation is a source failure",
                (await Run(fake)).Outcome == CollectorRunOutcome.SourceFailed && fake.Reads == 0);
            fake.CancelApprovalWithoutToken = false;
            fake.Approval = approval with { QueryPackSha256 = new string('c', 64) };
            Check("pack digest mismatch stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.InvalidApproval && fake.Reads == 0);
            fake.Approval = approval with { CheckpointPath = Path.Combine(directory, "wrong-place.enc") };
            Check("checkpoint outside run directory stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.InvalidApproval && fake.Reads == 0);
            fake.Approval = approval with { CheckpointPath = approval.RunStartPath };
            Check("run-start and checkpoint path collision stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.InvalidApproval && fake.Reads == 0);
            fake.Approval = approval with { ExtractionStartedAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
            Check("future extraction start stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.CheckpointRejected && fake.Reads == 0);
            fake.Approval = approval with
            {
                ExtractionStartedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                RunStartPath = Path.Combine(stageDirectory, "expired.igr")
            };
            Check("expired extraction stops before read",
                (await new CollectorRunCoordinator(fake).RunAsync(configPath,
                    config with { RetentionHours = 1 }, CancellationToken.None)).Outcome ==
                CollectorRunOutcome.Expired && fake.Reads == 0);
            fake.Approval = approval with
            {
                Query = approval.Query with { SupportedExactBuilds = ["10.0.0.2"] }
            };
            Check("unsupported build stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.UnsupportedSource && fake.Reads == 0);
            fake.Approval = approval with
            {
                Permission = new PermissionProbe(true, [SourceCapability.MinimumRead, SourceCapability.Write])
            };
            Check("write capability stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.PermissionBlocked && fake.Reads == 0);
            fake.Approval = approval;

            using (var lease = CrossProcessRunLease.TryAcquire(configPath, config.ScopeId))
            {
                Check("overlap skips before read",
                    lease is not null && (await Run(fake)).Outcome == CollectorRunOutcome.OverlapSkipped &&
                    fake.Reads == 0);
            }

            var oneRowConfig = config with { MaxRows = 1 };
            var partial = await new CollectorRunCoordinator(fake).RunAsync(configPath, oneRowConfig,
                CancellationToken.None);
            Check("first bounded page stops at row cap", partial.Outcome == CollectorRunOutcome.LimitReached &&
                partial.CompletedPages == 1 && partial.CompletedRows == 1);
            Check("only allowlisted value staged", fake.Staged.Count == 1 &&
                fake.Staged[0].Count == 2 && fake.Staged[0][0].IncludedValue == "uid-1" &&
                fake.Staged[0][1].IncludedValue is null);
            Check("checkpoint encrypted and present", File.Exists(approval.CheckpointPath) &&
                !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(approval.CheckpointPath)).Contains("uid-1",
                    StringComparison.Ordinal));
            Check("run start encrypted and present", File.Exists(approval.RunStartPath) &&
                !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(approval.RunStartPath)).Contains(
                    "schema-inventory", StringComparison.Ordinal));
            var before = fake.Reads;
            fake.Approval = approval with { ExtractionStartedAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
            var cappedResume = await new CollectorRunCoordinator(fake).RunAsync(configPath, oneRowConfig,
                CancellationToken.None);
            Check("row cap and run start persist across coordinator restart",
                cappedResume.Outcome == CollectorRunOutcome.LimitReached && cappedResume.CompletedRows == 1 &&
                fake.Reads == before);
            fake.Approval = approval;

            var resumed = await Run(fake);
            Check("resume from completed boundary", resumed.Outcome == CollectorRunOutcome.Completed &&
                resumed.CompletedPages == 2 && resumed.CompletedRows == 2 &&
                fake.RequestedBoundaries.Last() == "page-1" &&
                fake.Staged.Count == 2);
            before = fake.Reads;
            var stagedPath = Directory.GetFiles(stageDirectory, "*.stage")[0];
            var stagedBytes = File.ReadAllBytes(stagedPath);
            File.Delete(stagedPath);
            Check("missing checkpointed stage rejects before source read",
                (await Run(fake)).Outcome == CollectorRunOutcome.CheckpointRejected && fake.Reads == before);
            File.WriteAllBytes(stagedPath, stagedBytes);
            var alteredStage = (byte[])stagedBytes.Clone();
            alteredStage[^1] ^= 1;
            File.WriteAllBytes(stagedPath, alteredStage);
            Check("tampered checkpointed stage rejects before source read",
                (await Run(fake)).Outcome == CollectorRunOutcome.CheckpointRejected && fake.Reads == before);
            File.WriteAllBytes(stagedPath, stagedBytes);
            var terminalResume = await Run(fake);
            Check("terminal checkpoint completes without rereading source",
                terminalResume.Outcome == CollectorRunOutcome.Completed && terminalResume.CompletedRows == 2 &&
                fake.Reads == before);
            var savedRunStart = File.ReadAllBytes(approval.RunStartPath);
            File.Delete(approval.RunStartPath);
            Check("checkpoint without start fails closed",
                (await Run(fake)).Outcome == CollectorRunOutcome.CheckpointRejected && fake.Reads == before);
            File.WriteAllBytes(approval.RunStartPath, savedRunStart);
            var tighterResume = await new CollectorRunCoordinator(fake).RunAsync(configPath, oneRowConfig,
                CancellationToken.None);
            Check("completed ledger cannot exceed newly narrowed row cap",
                tighterResume.Outcome == CollectorRunOutcome.CheckpointRejected &&
                tighterResume.CompletedRows == 2 && fake.Reads == before);
            Check("page size bounded", fake.RequestedSizes.All(size => size <= 10));

            var wrongPolicy = approval with
            {
                FieldPolicy = approval.FieldPolicy with { Sha256 = new string('d', 64) }
            };
            fake.Approval = wrongPolicy;
            before = fake.Reads;
            Check("policy digest mismatch stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.InvalidApproval && fake.Reads == before);

            fake.Approval = approval with { CheckpointKey = RandomNumberGenerator.GetBytes(32) };
            Check("wrong checkpoint key stops before read",
                (await Run(fake)).Outcome == CollectorRunOutcome.CheckpointRejected && fake.Reads == before);

            fake.Approval = approval;
            File.Delete(approval.CheckpointPath);
            fake.Pages = [first with
            {
                Fields = [new FieldCandidate(included, FieldClassification.Prohibited, "secret")]
            }];
            before = fake.Staged.Count;
            Check("prohibited page never staged or checkpointed",
                (await Run(fake)).Outcome == CollectorRunOutcome.PageRejected &&
                fake.Staged.Count == before && !File.Exists(approval.CheckpointPath));

            fake.Pages = [first with { RowCount = config.MaxPageSize + 1 }];
            Check("oversized page rejected", (await Run(fake)).Outcome == CollectorRunOutcome.PageRejected);

            fake.Pages = [first];
            fake.FailRead = true;
            Check("read failure leaves no checkpoint",
                (await Run(fake)).Outcome == CollectorRunOutcome.SourceFailed &&
                !File.Exists(approval.CheckpointPath));
            fake.FailRead = false;
            fake.CancelReadWithoutToken = true;
            Check("unexpected read cancellation leaves no checkpoint",
                (await Run(fake)).Outcome == CollectorRunOutcome.SourceFailed &&
                !File.Exists(approval.CheckpointPath));
            fake.CancelReadWithoutToken = false;
            fake.FailStage = true;
            Check("stage failure leaves no checkpoint",
                (await Run(fake)).Outcome == CollectorRunOutcome.StageFailed &&
                !File.Exists(approval.CheckpointPath));
            fake.FailStage = false;
            fake.CancelStageWithoutToken = true;
            Check("unexpected stage cancellation leaves no checkpoint",
                (await Run(fake)).Outcome == CollectorRunOutcome.StageFailed &&
                !File.Exists(approval.CheckpointPath));
            fake.CancelStageWithoutToken = false;

            fake.BlockStageUntilCancellation = true;
            var stageTimedOut = await new CollectorRunCoordinator(fake).RunAsync(configPath,
                config with { MaxDurationSeconds = 1 }, CancellationToken.None);
            Check("stage deadline stops without advancing checkpoint",
                stageTimedOut.Outcome == CollectorRunOutcome.LimitReached &&
                stageTimedOut.CompletedPages == 0 && !File.Exists(approval.CheckpointPath));
            fake.BlockStageUntilCancellation = false;

            var stagedBeforeWriteFailure = fake.Staged.Count;
            var stagedAttemptsBeforeWriteFailure = fake.StageAttempts;
            var interruptedPage = new CollectorPage("interrupted-page", 1,
                [new FieldCandidate(included, FieldClassification.ApprovedReference, "uid-interrupted")], false);
            fake.Pages = [interruptedPage];
            fake.BlockCheckpointOnStage = true;
            var checkpointWriteFailure = await Run(fake);
            Check("staged page with failed checkpoint remains incomplete",
                checkpointWriteFailure.Outcome == CollectorRunOutcome.CheckpointRejected &&
                checkpointWriteFailure.CompletedPages == 0 && fake.Staged.Count == stagedBeforeWriteFailure + 1);
            fake.BlockCheckpointOnStage = false;
            Directory.Delete(approval.CheckpointPath);

            fake.Pages = [interruptedPage with
            {
                Fields = [new FieldCandidate(included, FieldClassification.ApprovedReference, "changed-value")]
            }];
            Check("changed restage conflicts before checkpoint",
                (await Run(fake)).Outcome == CollectorRunOutcome.StageFailed &&
                !File.Exists(approval.CheckpointPath));

            var restarted = new FakeAdapter(approval, [interruptedPage], stageDirectory, stageContext,
                config.MaxLocalBytes, key);
            var recovered = await Run(restarted);
            Check("fresh adapter reuses encrypted staged page and checkpoints it",
                recovered.Outcome == CollectorRunOutcome.Completed && recovered.CompletedPages == 1 &&
                fake.Staged.Count == stagedBeforeWriteFailure + 1 &&
                fake.StageAttempts == stagedAttemptsBeforeWriteFailure + 2 &&
                restarted.StageAttempts == 1 && restarted.Staged.Count == 0 &&
                EncryptedPageStageStore.Load(stageDirectory, stageContext, "interrupted-page", key) is not null &&
                EncryptedCheckpointStore.Load(approval.CheckpointPath, stageContext, key)?.Count == 1);
            File.Delete(approval.CheckpointPath);

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            before = fake.Reads;
            Check("canceled run makes no read",
                (await new CollectorRunCoordinator(fake).RunAsync(configPath, config, canceled.Token)).Outcome ==
                CollectorRunOutcome.Canceled && fake.Reads == before);

            fake.Approval = approval with
            {
                Permission = new PermissionProbe(true,
                    [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly])
            };
            fake.Pages = [first with { Boundary = "warning-page", HasMore = false }];
            Check("excess read-only warning survives run",
                (await Run(fake)).ExcessReadOnlyWarning);
            count += await StageReceiptChecksAsync(config, directory, configPath, approval, stageContext, first, second);
            return count;

            Task<CollectorRunResult> Run(FakeAdapter adapter) =>
                new CollectorRunCoordinator(adapter).RunAsync(configPath, config, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        void Check(string name, bool okay)
        {
            if (!okay)
            {
                throw new Exception($"{name}: unexpected run result.");
            }

            count++;
        }
    }

    private static async Task<int> StageReceiptChecksAsync(CollectorConfig config, string directory,
        string configPath, ApprovedCollectorRun approval, PageCheckpointContext context,
        CollectorPage first, CollectorPage second)
    {
        var count = 0;
        foreach (var fault in Enum.GetValues<ReceiptStageFault>().Where(fault => fault != ReceiptStageFault.None))
        {
            var adapter = NewAdapter([first with { HasMore = false }]);
            adapter.ReceiptFault = fault;
            var result = await Run(adapter);
            Check("invalid receipt refuses first checkpoint", result.Outcome == CollectorRunOutcome.StageFailed &&
                result.CompletedPages == 0 && result.CompletedRows == 0 && !result.ExcessReadOnlyWarning &&
                !File.Exists(adapter.Approval!.CheckpointPath) && adapter.StageAttempts == 1 && adapter.Reads == 1);
            Check("failure result is payload-free", !result.ToString().Contains(directory, StringComparison.Ordinal) &&
                !result.ToString().Contains("uid-1", StringComparison.Ordinal));
        }

        var exact = NewAdapter([first, second]);
        exact.AttemptFieldMutation = true;
        var completed = await Run(exact);
        Check("exact receipt completes two pages", completed.Outcome == CollectorRunOutcome.Completed &&
            completed.CompletedPages == 2 && completed.CompletedRows == 2);
        Check("adapter cannot mutate expected field sequence", exact.FieldMutationDenied && exact.ReadOnlyFieldsObserved &&
            EncryptedPageStageStore.Load(exact.StageDirectory, context, first.Boundary, exact.StageKey)?.Fields[0].IncludedValue == "uid-1");

        var later = NewAdapter([first, second]);
        var oneRow = config with { MaxRows = 1 };
        var partial = await Run(later, oneRow);
        Check("receipt prefix persisted", partial.Outcome == CollectorRunOutcome.LimitReached &&
            partial.CompletedPages == 1 && partial.CompletedRows == 1);
        var checkpointBefore = File.ReadAllBytes(later.Approval!.CheckpointPath);
        later.ReceiptFault = ReceiptStageFault.TamperedBytes;
        var laterFailure = await Run(later);
        Check("later receipt failure preserves prior counts", laterFailure.Outcome == CollectorRunOutcome.StageFailed &&
            laterFailure.CompletedPages == 1 && laterFailure.CompletedRows == 1);
        Check("later receipt failure preserves exact checkpoint bytes", checkpointBefore.SequenceEqual(File.ReadAllBytes(later.Approval.CheckpointPath)));

        var cancel = NewAdapter([first with { HasMore = false }]);
        using (var tokenSource = new CancellationTokenSource())
        {
            cancel.AfterStage = tokenSource.Cancel;
            var canceled = await Run(cancel, cancellationToken: tokenSource.Token);
            Check("cancellation after stage leaves no checkpoint", canceled.Outcome == CollectorRunOutcome.Canceled &&
                canceled.CompletedPages == 0 && canceled.CompletedRows == 0 && !File.Exists(cancel.Approval!.CheckpointPath));
        }
        cancel.AfterStage = null;
        var orphanPaths = Directory.GetFiles(cancel.StageDirectory, "*.stage");
        var orphanBefore = File.ReadAllBytes(orphanPaths.Single());
        var retried = await Run(cancel);
        Check("canceled orphan retries idempotently", retried.Outcome == CollectorRunOutcome.Completed &&
            retried.CompletedPages == 1 && cancel.Staged.Count == 1 &&
            Directory.GetFiles(cancel.StageDirectory, "*.stage").Length == 1 &&
            orphanBefore.SequenceEqual(File.ReadAllBytes(orphanPaths.Single())));

        var deadline = NewAdapter([first with { HasMore = false }]);
        deadline.ReturnAfterDeadlineOnStage = true;
        var limited = await Run(deadline, config with { MaxDurationSeconds = 1 });
        Check("deadline after durable stage leaves no checkpoint", limited.Outcome == CollectorRunOutcome.LimitReached &&
            limited.CompletedPages == 0 && limited.CompletedRows == 0 && !File.Exists(deadline.Approval!.CheckpointPath) &&
            Directory.GetFiles(deadline.StageDirectory, "*.stage").Length == 1);
        deadline.ReturnAfterDeadlineOnStage = false;
        Check("deadline orphan can recover", (await Run(deadline)).Outcome == CollectorRunOutcome.Completed && deadline.Staged.Count == 1);

        var cancelLater = NewAdapter([first, second]);
        _ = await Run(cancelLater, oneRow);
        var prefixBytes = File.ReadAllBytes(cancelLater.Approval!.CheckpointPath);
        using (var tokenSource = new CancellationTokenSource())
        {
            cancelLater.AfterStage = tokenSource.Cancel;
            var result = await Run(cancelLater, cancellationToken: tokenSource.Token);
            Check("later cancellation preserves prefix", result.Outcome == CollectorRunOutcome.Canceled &&
                result.CompletedPages == 1 && result.CompletedRows == 1 &&
                prefixBytes.SequenceEqual(File.ReadAllBytes(cancelLater.Approval.CheckpointPath)));
        }
        cancelLater.AfterStage = null;
        Check("later orphan retries without duplicating first page", (await Run(cancelLater)).Outcome == CollectorRunOutcome.Completed &&
            cancelLater.Staged.Count == 2 && EncryptedCheckpointStore.Load(cancelLater.Approval.CheckpointPath, context, cancelLater.StageKey)?.Count == 2);

        var empty = NewAdapter([new CollectorPage("synthetic-empty-terminal", 0, [], false)]);
        var emptyResult = await Run(empty);
        Check("existing synthetic empty terminal receipt remains supported", emptyResult.Outcome == CollectorRunOutcome.Completed &&
            emptyResult.CompletedPages == 1 && emptyResult.CompletedRows == 0);
        var emptyReads = empty.Reads;
        Check("empty terminal restart does not read", (await Run(empty)).Outcome == CollectorRunOutcome.Completed && empty.Reads == emptyReads);

        var warned = NewAdapter([first with { HasMore = false }]);
        warned.Approval = warned.Approval! with
        {
            Permission = new PermissionProbe(true, [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly])
        };
        warned.ReceiptFault = ReceiptStageFault.Missing;
        var warnedFailure = await Run(warned);
        Check("receipt failure preserves permission warning", warnedFailure.Outcome == CollectorRunOutcome.StageFailed &&
            warnedFailure.ExcessReadOnlyWarning && warnedFailure.CompletedPages == 0 && !File.Exists(warned.Approval.CheckpointPath));
        return count;

        FakeAdapter NewAdapter(IReadOnlyList<CollectorPage> pages)
        {
            var runId = Guid.NewGuid();
            var runDirectory = OperatingSystem.IsWindows() ? WindowsRunDirectoryProvisioner.ProvisionNew(directory, runId)
                : Directory.CreateDirectory(Path.Combine(directory, runId.ToString("N"))).FullName;
            var key = RandomNumberGenerator.GetBytes(32);
            var runApproval = approval with
            {
                RunStartPath = Path.Combine(runDirectory, "run-start.igr"),
                CheckpointPath = Path.Combine(runDirectory, "checkpoint.enc"),
                CheckpointKey = key,
                ExtractionStartedAtUtc = DateTimeOffset.UtcNow
            };
            return new FakeAdapter(runApproval, pages, runDirectory, context, config.MaxLocalBytes, key);
        }

        Task<CollectorRunResult> Run(FakeAdapter adapter, CollectorConfig? limits = null, CancellationToken cancellationToken = default) =>
            new CollectorRunCoordinator(adapter).RunAsync(configPath, limits ?? config, cancellationToken);

        void Check(string name, bool condition)
        {
            if (!condition) throw new Exception($"Stage receipt fixture {name} failed.");
            count++;
        }
    }

    private enum ReceiptStageFault
    {
        None, Missing, TamperedBytes, MalformedBytes, WrongCount, WrongTerminal, WrongContent,
        ReorderedFields, WrongContext, WrongBoundary, WrongKey
    }

    private sealed class FakeAdapter(ApprovedCollectorRun? approval, IReadOnlyList<CollectorPage> pages,
        string stageDirectory, PageCheckpointContext stageContext, long maxLocalBytes, byte[] stageKey)
        : ICollectorRunAdapter
    {
        public ApprovedCollectorRun? Approval { get; set; } = approval;
        public IReadOnlyList<CollectorPage> Pages { get; set; } = pages;
        public int Loads { get; private set; }
        public int Reads { get; private set; }
        public List<string?> RequestedBoundaries { get; } = [];
        public List<int> RequestedSizes { get; } = [];
        public List<IReadOnlyList<MinimizedField>> Staged { get; } = [];
        public int StageAttempts { get; private set; }
        public bool FailRead { get; set; }
        public bool FailStage { get; set; }
        public bool CancelApprovalWithoutToken { get; set; }
        public bool CancelReadWithoutToken { get; set; }
        public bool CancelStageWithoutToken { get; set; }
        public bool BlockStageUntilCancellation { get; set; }
        public bool BlockCheckpointOnStage { get; set; }
        public ReceiptStageFault ReceiptFault { get; set; }
        public bool AttemptFieldMutation { get; set; }
        public bool FieldMutationDenied { get; private set; }
        public bool ReadOnlyFieldsObserved { get; private set; }
        public bool ReturnAfterDeadlineOnStage { get; set; }
        public Action? AfterStage { get; set; }
        public string StageDirectory => stageDirectory;
        public byte[] StageKey => stageKey;

        public Task<ApprovedCollectorRun?> LoadApprovedRunAsync(CollectorConfig config,
            CancellationToken cancellationToken)
        {
            Loads++;
            if (CancelApprovalWithoutToken)
            {
                throw new OperationCanceledException("Synthetic approval cancellation.");
            }
            return Task.FromResult(Approval);
        }

        public Task<CollectorPage> ReadPageAsync(string? afterBoundary, int pageSize,
            CancellationToken cancellationToken)
        {
            Reads++;
            if (FailRead)
            {
                throw new IOException("Synthetic read failure.");
            }
            if (CancelReadWithoutToken)
            {
                throw new OperationCanceledException("Synthetic read cancellation.");
            }

            RequestedBoundaries.Add(afterBoundary);
            RequestedSizes.Add(pageSize);
            var index = afterBoundary is null ? 0 :
                Pages.ToList().FindIndex(page => page.Boundary == afterBoundary) + 1;
            return Task.FromResult(Pages[Math.Min(index, Pages.Count - 1)]);
        }

        public async Task StagePageAsync(string runDirectory, string boundary, int rowCount, bool isTerminal, string digest,
            IReadOnlyList<MinimizedField> fields,
            CancellationToken cancellationToken)
        {
            if (!string.Equals(runDirectory, stageDirectory,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new IOException("Synthetic stage directory differs from the approved run directory.");
            }
            if (FailStage)
            {
                throw new IOException("Synthetic stage failure.");
            }
            if (CancelStageWithoutToken)
            {
                throw new OperationCanceledException("Synthetic stage cancellation.");
            }

            if (BlockStageUntilCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            StageAttempts++;
            if (AttemptFieldMutation)
            {
                ReadOnlyFieldsObserved = fields is ICollection<MinimizedField> { IsReadOnly: true };
                try
                {
                    ((IList<MinimizedField>)fields)[0] = fields[0] with { IncludedValue = "synthetic-mutation" };
                }
                catch (NotSupportedException)
                {
                    FieldMutationDenied = true;
                }
            }
            if (ReceiptFault == ReceiptStageFault.Missing) return;

            var storedContext = ReceiptFault == ReceiptStageFault.WrongContext
                ? stageContext with { QueryId = "synthetic-wrong-query" } : stageContext;
            var storedBoundary = ReceiptFault == ReceiptStageFault.WrongBoundary ? boundary + "-different" : boundary;
            var storedRows = ReceiptFault == ReceiptStageFault.WrongCount ? rowCount + 1 : rowCount;
            var storedTerminal = ReceiptFault == ReceiptStageFault.WrongTerminal ? !isTerminal : isTerminal;
            var storedFields = ReceiptFault switch
            {
                ReceiptStageFault.WrongContent => fields.Select((field, index) => index == 0
                    ? field with { IncludedValue = "synthetic-substitution" } : field).ToArray(),
                ReceiptStageFault.ReorderedFields => fields.Reverse().ToArray(),
                _ => fields
            };
            var storedKey = ReceiptFault == ReceiptStageFault.WrongKey ? RandomNumberGenerator.GetBytes(32) : stageKey;
            var storedDigest = ReceiptFault == ReceiptStageFault.None ? digest : MinimizedPageDigest.Compute(storedBoundary, storedRows, storedTerminal, storedFields);
            var priorPaths = Directory.GetFiles(stageDirectory, "*.stage");
            if (EncryptedPageStageStore.Stage(stageDirectory, storedContext, storedBoundary, storedRows, storedTerminal, storedDigest,
                    storedFields, maxLocalBytes, storedKey) == EncryptedPageStageStore.StageResult.NewPage)
            {
                Staged.Add(fields);
            }
            if (ReceiptFault is ReceiptStageFault.TamperedBytes or ReceiptStageFault.MalformedBytes)
            {
                var path = Directory.GetFiles(stageDirectory, "*.stage").Except(priorPaths).Single();
                if (ReceiptFault == ReceiptStageFault.MalformedBytes) File.WriteAllBytes(path, [1, 2, 3]);
                else
                {
                    var bytes = File.ReadAllBytes(path);
                    bytes[^1] ^= 1;
                    File.WriteAllBytes(path, bytes);
                }
            }
            AfterStage?.Invoke();
            if (ReturnAfterDeadlineOnStage)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            }
            if (BlockCheckpointOnStage && Approval is not null)
            {
                Directory.CreateDirectory(Approval.CheckpointPath);
            }
        }
    }
}

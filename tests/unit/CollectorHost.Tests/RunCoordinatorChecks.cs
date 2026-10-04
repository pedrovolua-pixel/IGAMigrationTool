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
            if (EncryptedPageStageStore.Stage(stageDirectory, stageContext, boundary, rowCount, isTerminal, digest,
                    fields, maxLocalBytes, stageKey) == EncryptedPageStageStore.StageResult.NewPage)
            {
                Staged.Add(fields);
            }
            if (BlockCheckpointOnStage && Approval is not null)
            {
                Directory.CreateDirectory(Approval.CheckpointPath);
            }
        }
    }
}

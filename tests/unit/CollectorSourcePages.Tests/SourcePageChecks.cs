using CollectorSafety;
using CollectorSourcePages;

namespace CollectorSourcePages.Tests;

internal static class SourcePageChecks
{
    internal static async Task<int> RunAsync()
    {
        var count = 0;
        var fixture = new ScriptedSourcePorts();
        var kernel = fixture.Kernel();
        var result = await kernel.RunPageAsync(fixture.Request(), default);
        Check("full conservative page", result.Outcome == SourcePageOutcome.PageReady && result.Page is { Terminal: false, Rows.Count: 2, NextContinuation.IntegerValue: 0 } && result.Counters.ReadCalls == 2);
        Check("actual registered connection probe", ReferenceEquals(fixture.LastConnection, fixture.ProbedConnection));
        var expectedTrace = new[] { "resolve", "history", "open", "revalidate", "probe", "impact", "execute", "schema", "impact", "read", "classify", "classify", "classify", "compare", "impact", "read", "classify", "classify", "classify", "compare", "compare", "compare", "revalidate", "impact", "dispose-reader", "dispose-connection" };
        Check("literal source gate call order", fixture.Trace.SequenceEqual(expectedTrace));
        Check("bounded command has first only parameter", fixture.LastCommand is { Phase: SourceQueryPhase.First, Boundary: null, PageSize.Value.IntegerValue: 2 } && fixture.LastCommand.Sql == fixture.Pair.FirstSql && fixture.LastCommand.Timeout > TimeSpan.Zero);
        Check("no inferred uid merge", result.Page!.Conflicts is [{ Kind: SourceConflictKind.ObjectUid, First.RowOrdinal: 0, Second.RowOrdinal: 1 }] && result.Page.Rows.Count == 2);
        Check("opaque identity provenance", result.Page.Identity.Scope.ExtractionId == fixture.Scope.ExtractionId && result.Page.Identity.PairId == fixture.Pair.PairId && result.Page.Identity.PageOrdinal == 0);
        var field = result.Page.Rows[1].Fields[1];
        Check("exact trusted field provenance", field.Provenance.Scope.ExtractionId == fixture.Scope.ExtractionId && field.Provenance.Phase == SourceQueryPhase.First && field.Provenance.PageOrdinal == 0 && field.Provenance.RowOrdinal == 1 && field.Provenance.FieldOrdinal == 1 && field.Provenance.Field == "U" && field.Provenance.SqlType == "varchar(20)" && field.Provenance.ExactBuild == "fixture-build" && field.Provenance.InstalledModules is [{ ModuleId: "fixture-module", ExactVersion: "fixture-module-build" }] && field.Provenance.ExtractedAtUtc == fixture.Clock.GetUtcNow() && field.Provenance.PolicyVersion == "policy-v1" && field.Provenance.SchemaVersion == "schema-v1" && field.Provenance.NormalizationVersion == "normalization-v1");
        fixture.History = new(SourceHistoryState.Previous, result.Receipt!.OriginalStartedAtUtc, result.Receipt);
        fixture.Rows = [];
        var continuation = fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: result.Page.NextContinuation);
        var empty = await kernel.RunPageAsync(continuation, default);
        Check("full then explicit empty terminal", empty.Outcome == SourcePageOutcome.PageReady && empty.Page is { Terminal: true, Rows.Count: 0, NextContinuation: null, Identity.PageOrdinal: 1 } && empty.Counters.ReadCalls == 1 && empty.Receipt!.CumulativeRows == 2);
        Check("continuation exact typed binding", fixture.LastCommand is { Phase: SourceQueryPhase.Continuation, Boundary.Value.SqlType: "int", Boundary.Value.IntegerValue: 0 } && fixture.LastCommand.Generation != Guid.Empty);
        var generation = fixture.LastCommand!.Generation;
        var retry = await kernel.RunPageAsync(continuation, default);
        Check("retry stable identity fresh generation gates", retry.Page!.Identity.PageOrdinal == empty.Page!.Identity.PageOrdinal && retry.Page.Identity.Scope.ExtractionId == empty.Page.Identity.Scope.ExtractionId && fixture.LastCommand!.Generation != generation && retry.Counters.PermissionProbes == 1 && retry.Counters.AuthorityRevalidations == 2);
        fixture.History = new(SourceHistoryState.Previous, empty.Receipt!.OriginalStartedAtUtc, empty.Receipt);
        var afterTerminal = await kernel.RunPageAsync(fixture.Request(ordinal: 2, phase: SourceQueryPhase.Continuation, continuation: SourceNativeValue.Integer("int", 0)), default);
        Denied("terminal cannot continue", afterTerminal, SourcePageReason.HistoryMismatch);
        Check("terminal no source open", afterTerminal.Counters.ConnectionOpens == 0);

        foreach (var key in new[] { int.MinValue, -1, 0, int.MaxValue })
        {
            fixture = new(); fixture.Rows = [ScriptedSourcePorts.Row(0, key)];
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("first no sentinel minimum integer", result.Page is { Terminal: true, Rows.Count: 1, NextContinuation: null } && result.Page.Rows[0].Fields[0].Value!.IntegerValue == key && result.Counters.ReadCalls == 2);
        }
        fixture = new(); fixture.Rows = [];
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("empty first explicit identity", result.Page is { Terminal: true, Rows.Count: 0, Identity.PageOrdinal: 0, NextContinuation: null } && result.Counters.ReadCalls == 1);
        fixture = new();
        var capLimits = fixture.Limits(maximumRows: 10);
        fixture.Rows = Enumerable.Range(0, 10).Select(i => ScriptedSourcePorts.Row(i, i, "uid" + i)).ToArray();
        result = await fixture.Kernel().RunPageAsync(fixture.Request(size: 10, limits: capLimits), default);
        Check("full row cap partial no lookahead", result is { Outcome: SourcePageOutcome.Partial, Reason: SourcePageReason.RowCap, Page.Terminal: false, Page.Rows.Count: 10, Page.NextContinuation: null, Receipt.RowCap: true } && result.Counters.ReadCalls == 10);
        fixture.History = new(SourceHistoryState.Previous, result.Receipt!.OriginalStartedAtUtc, result.Receipt);
        var capped = await fixture.Kernel().RunPageAsync(fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: SourceNativeValue.Integer("int", 9), limits: capLimits), default);
        Denied("row cap receipt cannot permit more", capped, SourcePageReason.HistoryMismatch);
        Check("capped no subsequent query", capped.Counters.Executions == 0 && capped.Counters.ConnectionOpens == 0);

        foreach (var state in new[] { SourceAuthorityState.Missing, SourceAuthorityState.Revoked, SourceAuthorityState.Drifted, SourceAuthorityState.Unsupported, (SourceAuthorityState)99 })
        {
            fixture = new() { AuthorityState = state };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("initial authority closed", result.Page is null && result.Counters.Executions == 0 && result.Counters.ConnectionOpens == 0);
        }
        foreach (var capability in new[] { SourceCapability.Write, SourceCapability.Ddl, SourceCapability.Ownership, SourceCapability.Impersonation,
            SourceCapability.SecurityAdministration, SourceCapability.ServerAdministration, SourceCapability.AgentOrJobAdministration,
            SourceCapability.BackupOrRestore, SourceCapability.Unknown, (SourceCapability)999 })
        {
            fixture = new() { Capabilities = [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly, capability] };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Denied("capability denies before execute", result, SourcePageReason.PermissionBlocked);
            Check("blocked proof no query or warning", result.Counters.Executions == 0 && fixture.LastCommand is null && result.Counters.WarningAudits == 0);
        }
        foreach (var missingMinimum in new[] { true, false })
        {
            fixture = new() { MinimumSatisfied = !missingMinimum, Capabilities = missingMinimum ? [SourceCapability.MinimumRead] : [] };
            Denied("minimum read proof required", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.PermissionBlocked);
        }
        fixture = new() { PermissionSubstitution = true };
        Denied("permission generation substitution", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.PermissionBlocked);
        fixture = new() { UnregisterBeforeProbe = true };
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("UUID alone not probe evidence", result.Counters.Executions == 0 && result.Page is null && fixture.ProbedConnection is null);
        foreach (var auditFailure in new[] { 0, 1 })
        {
            fixture = new() { Capabilities = [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly], AuditMissing = auditFailure == 0, AuditSubstitution = auditFailure == 1 };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Denied("warning receipt unavailable or substituted", result, SourcePageReason.WarningAuditMissing);
            Check("warning before evidence gate", result.Counters.WarningAudits == 1 && result.Counters.Executions == 0 && fixture.Trace.IndexOf("warning") > fixture.Trace.IndexOf("probe"));
        }
        fixture = new() { Capabilities = [SourceCapability.MinimumRead, SourceCapability.ExcessReadOnly] };
        kernel = fixture.Kernel(); result = await kernel.RunPageAsync(fixture.Request(), default);
        Check("warning committed before evidence", result.Page is not null && fixture.Trace.IndexOf("warning") < fixture.Trace.IndexOf("execute") && fixture.LastWarning!.Generation == fixture.LastCommand!.Generation);
        var warningGeneration = fixture.LastWarning!.Generation;
        fixture.Trace.Clear(); result = await kernel.RunPageAsync(fixture.Request(), default);
        Check("fresh retry warning proof", result.Counters.WarningAudits == 1 && result.Counters.PermissionProbes == 1 && fixture.LastWarning!.Generation != warningGeneration);
        fixture.RevalidationState = SourceAuthorityState.Revoked;
        result = await kernel.RunPageAsync(fixture.Request(), default);
        Denied("revoked on reopened source", result, SourcePageReason.AuthorityRevoked);
        Check("reconnect revocation no execute", result.Counters.Executions == 0 && result.Counters.PermissionProbes == 0);
        fixture = new(); fixture.Hooks["read"] = () => fixture.RevalidationState = SourceAuthorityState.Revoked;
        Denied("revocation before admission", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.AuthorityRevoked);
        fixture = new(); fixture.Hooks["probe"] = () => fixture.LastConnection!.ReplaceGeneration();
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("generation drift refuses query", result.Page is null && result.Counters.Executions == 0);

        foreach (var kind in new[] { SourceImpactState.Unknown, SourceImpactState.Stop, (SourceImpactState)99 })
        {
            fixture = new() { ImpactState = kind };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("impact closed before execute", result.Outcome == SourcePageOutcome.Partial && result.Page is null && result.Counters.Executions == 0);
        }
        fixture = new(); fixture.Hooks["read"] = () => fixture.ImpactState = SourceImpactState.Stop;
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("impact stops before extra protected read", result.Page is null && result.Reason == SourcePageReason.ImpactStopped && result.Counters.ReadCalls == 1);

        foreach (var duplicate in new[] { true, false })
        {
            fixture = new(); fixture.Rows = [ScriptedSourcePorts.Row(0, 0), ScriptedSourcePorts.Row(1, duplicate ? 0 : -1)];
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Denied("paging progress conflict", result, duplicate ? SourcePageReason.PagingKeyConflict : SourcePageReason.PagingOrderViolation);
            Check("both paging occurrence refs no values", result.Conflicts is [{ First.RowOrdinal: 0, Second.RowOrdinal: 1 }] && result.Conflicts[0].First.Page.Scope.ExtractionId == fixture.Scope.ExtractionId);
        }
        fixture = new(); result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        fixture.History = new(SourceHistoryState.Previous, result.Receipt!.OriginalStartedAtUtc, result.Receipt);
        foreach (var key in new[] { 0, -1 })
        {
            fixture.Rows = [ScriptedSourcePorts.Row(0, key)];
            var violation = await fixture.Kernel().RunPageAsync(fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: result.Receipt.NextContinuation), default);
            Check("boundary conflict retains prior actual row", violation.Page is null && violation.Conflicts is [{ First.Page.PageOrdinal: 0, First.RowOrdinal: 1, Second.Page.PageOrdinal: 1, Second.RowOrdinal: 0 }]);
        }
        fixture = new() { UnsupportedPaging = true };
        Denied("no default SQL ordering", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.NativeOrderUnsupported);
        foreach (var mismatch in new[] { "comparison-binding", "comparison-ordinal", "classification-binding", "classification-ordinal" })
        {
            fixture = new(); var wrong = fixture.MakeBinding(policyRevision: 2);
            if (mismatch == "comparison-binding") fixture.ComparisonBinding = wrong;
            if (mismatch == "comparison-ordinal") fixture.ComparisonOrdinalDelta = 1;
            if (mismatch == "classification-binding") fixture.ClassificationBinding = wrong;
            if (mismatch == "classification-ordinal") fixture.ClassificationOrdinalDelta = 1;
            Denied("trusted decision exact binding", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.RegistryMismatch);
        }
        foreach (var disposition in new[] { FieldDisposition.Prohibited, FieldDisposition.Unclassified, (FieldDisposition)99 })
        {
            fixture = new() { Classifier = (ordinal, _) => ordinal == 1 ? disposition : FieldDisposition.Included };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("forbidden content no typed result", result.Page is null && result.Receipt is null && result.Counters.ReadCalls == 1 && result.Outcome is SourcePageOutcome.Quarantined or SourcePageOutcome.Refused);
        }
        foreach (var disposition in new[] { FieldDisposition.Redacted, FieldDisposition.Excluded })
        {
            fixture = new() { Classifier = (ordinal, _) => ordinal == 1 ? disposition : FieldDisposition.Included };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("returned minimized markers erase raw values", result.Page is not null && result.Page.Rows.All(r => r.Fields[1].Disposition == disposition && r.Fields[1].Value is null) && result.Page.Conflicts.Count == 0);
            fixture = new() { Classifier = (ordinal, _) => ordinal == 0 ? disposition : FieldDisposition.Included };
            Denied("key cannot be redacted or excluded", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.ClassifiedContent);
        }
        foreach (var classification in new[] { FieldClassification.Redacted, FieldClassification.Prohibited, FieldClassification.Unknown })
        {
            fixture = new(); var changedFields = fixture.Descriptor.Fields.Select((f, i) => i == 1 ? f with { Classification = classification } : f).ToArray();
            fixture.Pair = fixture.MakePair(fixture.Descriptor with { Fields = changedFields });
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("declared field denied before open with gap", result.Page is null && result.Counters.ConnectionOpens == 0 && result.Counters.Executions == 0 && result.PlannedGaps is [{ FieldOrdinal: 1 }]);
        }
        fixture = new();
        var excludedExpected = fixture.Expected with { Policy = fixture.Expected.Policy with { IncludedFields = new HashSet<FieldKey> { new("fixture-category", "K"), new("fixture-category", "V") } } };
        fixture.Binding = fixture.MakeBinding(expected: excludedExpected);
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("locked policy excluded declaration gap", result.Counters.ConnectionOpens == 0 && result.PlannedGaps is [{ Disposition: FieldDisposition.Excluded, FieldOrdinal: 1 }]);

        var badSchemas = new Func<ScriptedSourcePorts, SourceReturnedSchema>[]
        {
            _ => new([]), f => new(f.Schema.Fields.Take(2)), f => new(f.Schema.Fields.Reverse()),
            f => new(f.Schema.Fields.Concat([new("Extra", "int", false)])),
            f => new([f.Schema.Fields[0], f.Schema.Fields[0], f.Schema.Fields[2]]),
            f => new([new("k", "int", false), f.Schema.Fields[1], f.Schema.Fields[2]]),
            f => new([new("K", "bigint", false), f.Schema.Fields[1], f.Schema.Fields[2]]),
            f => new([new("K", "int", true), f.Schema.Fields[1], f.Schema.Fields[2]]),
            f => new([f.Schema.Fields[0], new("U", "varchar(19)", false), f.Schema.Fields[2]])
        };
        foreach (var bad in badSchemas)
        {
            fixture = new(); fixture.Schema = bad(fixture);
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Denied("native schema exact order type nullability", result, SourcePageReason.SchemaMismatch);
            Check("schema before any values", result.Counters.ReadCalls == 0 && result.Counters.ValueClassifications == 0);
        }
        foreach (var badRow in new[] { new SourceReturnedRow(1, [SourceNativeValue.Integer("int", 0)]),
            new SourceReturnedRow(0, [SourceNativeValue.Integer("int", 0)]),
            new SourceReturnedRow(0, [SourceNativeValue.SqlNull("int"), SourceNativeValue.Text("varchar(20)", "uid", 3), SourceNativeValue.SqlNull("varbinary(8)")]) })
        {
            fixture = new(); fixture.Rows = [badRow];
            Check("row shape/null key denied", (await fixture.Kernel().RunPageAsync(fixture.Request(), default)).Page is null);
        }
        var nativeFields = new[] { new QueryPackField("K", "int", false, FieldClassification.ApprovedReference),
            new("Tiny", "tinyint", false, FieldClassification.ApprovedReference), new("Small", "smallint", false, FieldClassification.ApprovedReference),
            new("Big", "bigint", false, FieldClassification.ApprovedReference), new("Uid", "varchar(20)", false, FieldClassification.ApprovedReference),
            new("G", "uniqueidentifier", false, FieldClassification.ApprovedReference), new("W", "nvarchar(2)", false, FieldClassification.ApprovedReference),
            new("B", "binary(2)", false, FieldClassification.ApprovedReference), new("N", "varbinary(8)", true, FieldClassification.ApprovedReference) };
        var guid = Guid.Parse("70000000-0000-0000-0000-000000000007"); var originalBytes = new byte[] { 1, 2 };
        var nativeValues = new[] { SourceNativeValue.Integer("int", int.MinValue), SourceNativeValue.Integer("tinyint", 255),
            SourceNativeValue.Integer("smallint", short.MinValue), SourceNativeValue.Integer("bigint", long.MinValue),
            SourceNativeValue.Text("varchar(20)", "native-string-uid", 17), SourceNativeValue.UniqueIdentifier(guid),
            SourceNativeValue.Text("nvarchar(2)", "\ud83d\ude00", 4), SourceNativeValue.Binary("binary(2)", originalBytes), SourceNativeValue.SqlNull("varbinary(8)") };
        fixture = TypedFixture(nativeFields, nativeValues); originalBytes[0] = 99;
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("native values preserve typed payloads", result.Page is { Rows.Count: 1, Terminal: true } && result.Page.Rows[0].Fields[3].Value!.IntegerValue == long.MinValue && result.Page.Rows[0].Fields[4].Value!.Kind == SourceNativeKind.Text && result.Page.Rows[0].Fields[4].Value!.TextValue == "native-string-uid" && result.Page.Rows[0].Fields[5].Value!.UniqueIdentifierValue == guid && result.Page.Rows[0].Fields[8].Value!.Kind == SourceNativeKind.SqlNull);
        var outputCopy = result.Page!.Rows[0].Fields[7].Value!.BinaryValue!; outputCopy[0] = 88;
        Check("binary inputs and outputs copied", result.Page.Rows[0].Fields[7].Value!.BinaryValue![0] == 1);
        var invalidValues = new (string Type, SourceNativeValue Value)[]
        {
            ("tinyint", SourceNativeValue.Integer("tinyint", -1)), ("tinyint", SourceNativeValue.Integer("tinyint", 256)),
            ("smallint", SourceNativeValue.Integer("smallint", short.MinValue - 1L)), ("smallint", SourceNativeValue.Integer("smallint", short.MaxValue + 1L)),
            ("int", SourceNativeValue.Integer("int", int.MinValue - 1L)), ("int", SourceNativeValue.Integer("int", int.MaxValue + 1L)),
            ("int", SourceNativeValue.Integer("bigint", 0)), ("uniqueidentifier", SourceNativeValue.Integer("int", 0)),
            ("varchar(2)", SourceNativeValue.Text("varchar(2)", "abc", 3)), ("varchar(2)", SourceNativeValue.Text("varchar(2)", "ab", 1)),
            ("varchar(2)", SourceNativeValue.Text("varchar(2)", "a", -1)), ("varchar(2)", SourceNativeValue.Text("varchar(2)", "", 1)),
            ("nvarchar(2)", SourceNativeValue.Text("nvarchar(2)", "ab", 2)), ("nvarchar(2)", SourceNativeValue.Text("nvarchar(2)", "abc", 6)),
            ("nvarchar(2)", SourceNativeValue.Text("nvarchar(2)", "\ud800", 2)), ("binary(2)", SourceNativeValue.Binary("binary(2)", [1])),
            ("varbinary(2)", SourceNativeValue.Binary("varbinary(2)", [1, 2, 3])), ("int", SourceNativeValue.SqlNull("int"))
        };
        foreach (var invalid in invalidValues)
        {
            fixture = TypedFixture([new("K", "int", false, FieldClassification.ApprovedReference), new("F", invalid.Type, false, FieldClassification.ApprovedReference)], [SourceNativeValue.Integer("int", 0), invalid.Value]);
            Denied("native width payload bound closed", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.NativeValueInvalid);
        }
        var valuesList = new List<SourceNativeValue> { SourceNativeValue.Integer("int", 0), SourceNativeValue.Text("varchar(20)", "uid", 3), SourceNativeValue.SqlNull("varbinary(8)") };
        var immutableRow = new SourceReturnedRow(0, valuesList); valuesList.Clear();
        fixture = new(); fixture.Rows = [immutableRow];
        Check("row collection frozen", (await fixture.Kernel().RunPageAsync(fixture.Request(), default)).Page!.Rows.Count == 1);
        fixture = new(); var mutableFields = fixture.Descriptor.Fields.ToList(); var mutableParams = fixture.Descriptor.Parameters.ToList();
        fixture.Pair = fixture.MakePair(fixture.Descriptor with { Fields = mutableFields, Parameters = mutableParams }); mutableFields.Clear(); mutableParams.Clear();
        Check("query fields parameters frozen", (await fixture.Kernel().RunPageAsync(fixture.Request(), default)).Page is not null);
        fixture = new(); var mutablePolicy = fixture.Expected.Policy.IncludedFields.ToHashSet(); var mutableModules = fixture.Expected.Source.InstalledModules.ToList();
        fixture.Binding = fixture.MakeBinding(expected: fixture.Expected with { Source = fixture.Expected.Source with { InstalledModules = mutableModules }, Policy = fixture.Expected.Policy with { IncludedFields = mutablePolicy } });
        mutablePolicy.Clear(); mutableModules.Clear();
        Check("registry modules and policy frozen", (await fixture.Kernel().RunPageAsync(fixture.Request(), default)).Page is not null);

        foreach (var bound in new[] { "field", "page", "total" })
        {
            fixture = new();
            var limits = bound == "field" ? fixture.Limits(fieldBytes: 2) : bound == "page" ? fixture.Limits(fieldBytes: 20, pageBytes: 20) : fixture.Limits(fieldBytes: 20, pageBytes: 25, totalBytes: 25);
            result = await fixture.Kernel().RunPageAsync(fixture.Request(limits: limits), default);
            Check("native byte cap no partial typed outputs", result.Page is null && result.Receipt is null && result.Outcome == SourcePageOutcome.Partial && result.Reason == (bound == "field" ? SourcePageReason.FieldByteCap : bound == "page" ? SourcePageReason.PageByteCap : SourcePageReason.PageByteCap));
        }
        // Total cap crosses a valid prior page without widening page limits.
        fixture = new(); var byteLimits = fixture.Limits(fieldBytes: 20, pageBytes: 40, totalBytes: 40);
        fixture.Rows = [ScriptedSourcePorts.Row(0, -1), ScriptedSourcePorts.Row(1, 0)];
        result = await fixture.Kernel().RunPageAsync(fixture.Request(limits: byteLimits), default);
        fixture.History = new(SourceHistoryState.Previous, result.Receipt!.OriginalStartedAtUtc, result.Receipt);
        fixture.Rows = [ScriptedSourcePorts.Row(0, 1), ScriptedSourcePorts.Row(1, 2)];
        var totalCap = await fixture.Kernel().RunPageAsync(fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: result.Receipt.NextContinuation, limits: byteLimits), default);
        Check("cumulative total byte cap", totalCap.Page is null && totalCap.Reason == SourcePageReason.TotalByteCap && totalCap.Counters.ReadCalls == 1);

        foreach (var stage in new[] { "resolve", "history", "open", "probe", "execute", "read" })
        {
            fixture = new(); using var cancel = new CancellationTokenSource(); fixture.Hooks[stage] = cancel.Cancel;
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), cancel.Token);
            Check("caller cancellation at every port", result.Outcome == SourcePageOutcome.Canceled && result.Page is null && result.Receipt is null);
            Check("cancel no extra protected reads", result.Counters.ReadCalls <= 1);
        }
        fixture = new(); using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel(); result = await fixture.Kernel().RunPageAsync(fixture.Request(), cancel.Token);
            Check("entry cancellation no ports", result.Outcome == SourcePageOutcome.Canceled && fixture.Trace.Count == 0);
        }
        foreach (var stage in new[] { "open", "probe", "execute", "read" })
        {
            fixture = new() { Time = TimeProvider.System, HoldAt = stage }; fixture.History = new(SourceHistoryState.Initial, fixture.Time.GetUtcNow(), null);
            result = await fixture.Kernel().RunPageAsync(fixture.Request(limits: fixture.Limits(timeout: TimeSpan.FromMilliseconds(40))), default);
            Check("actual cancelable finite attempt timeout", result.Outcome == SourcePageOutcome.TimedOut && result.Page is null && result.Counters.ReadCalls <= 1);
        }
        foreach (var stage in new[] { "open", "probe", "read", "revalidate", "dispose-reader" })
        {
            fixture = new(); fixture.Descriptor = fixture.Descriptor with { MaximumDuration = TimeSpan.FromHours(2) };
            fixture.Pair = fixture.MakePair(); fixture.Binding = fixture.MakeBinding();
            fixture.History = new(SourceHistoryState.Initial, fixture.Clock.GetUtcNow() - TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1), null);
            fixture.Hooks[stage] = () => fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            result = await fixture.Kernel().RunPageAsync(fixture.Request(limits: fixture.Limits(duration: TimeSpan.FromHours(2))), default);
            Check("original retention crossing no admission", result.Outcome == SourcePageOutcome.Expired && result.Page is null && result.Receipt is null);
        }
        foreach (var stage in new[] { "open", "probe", "read" })
        {
            fixture = new(); fixture.Hooks[stage] = () => { fixture.Clock.MoveUtc(TimeSpan.FromHours(-1)); fixture.Clock.AdvanceMonotonic(TimeSpan.FromSeconds(2)); };
            result = await fixture.Kernel().RunPageAsync(fixture.Request(limits: fixture.Limits(timeout: TimeSpan.FromSeconds(1))), default);
            Check("clock rollback cannot extend admission", result.Outcome == SourcePageOutcome.TimedOut && result.Page is null && result.Receipt is null);
        }
        fixture = new(); fixture.History = new(SourceHistoryState.Initial, fixture.Clock.GetUtcNow().AddSeconds(1), null);
        Denied("future original start rejected", await fixture.Kernel().RunPageAsync(fixture.Request(), default), SourcePageReason.HistoryMismatch);
        fixture = new(); fixture.History = new(SourceHistoryState.Initial, fixture.Clock.GetUtcNow().AddHours(-2), null);
        result = await fixture.Kernel().RunPageAsync(fixture.Request(limits: fixture.Limits(duration: TimeSpan.FromHours(3))), default);
        Check("expired initial no open", result.Outcome == SourcePageOutcome.Expired && result.Counters.ConnectionOpens == 0);
        fixture = new(); fixture.History = new(SourceHistoryState.Initial, fixture.Clock.GetUtcNow().AddMinutes(-20), null);
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("original run duration no open", result.Outcome == SourcePageOutcome.TimedOut && result.Counters.ConnectionOpens == 0);

        foreach (var stage in new[] { "resolve", "probe", "execute", "read" })
        {
            fixture = new(); fixture.Hooks[stage] = () => throw new InvalidOperationException("PROTECTED-FIXTURE-PAYLOAD");
            result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
            Check("exceptions metadata only", result.Page is null && result.Receipt is null && result.ToString() == "SourcePageResult" && result.Outcome is SourcePageOutcome.Refused or SourcePageOutcome.Disconnected);
        }
        fixture = new() { DisposalFailure = true };
        result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        Check("cleanup failure discards provisional outputs", result.Page is null && result.Receipt is null && result.Outcome == SourcePageOutcome.Disconnected && fixture.Trace.Contains("dispose-reader") && fixture.Trace.Contains("dispose-connection"));
        fixture = new(); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var cancel = new CancellationTokenSource())
        {
            fixture.HoldAt = "open"; fixture.Hooks["open"] = () => started.SetResult(); kernel = fixture.Kernel();
            var active = kernel.RunPageAsync(fixture.Request(), cancel.Token).AsTask(); await started.Task;
            var concurrent = await kernel.RunPageAsync(fixture.Request(), default);
            Check("single kernel concurrency refuses before open", concurrent.Reason == SourcePageReason.ConcurrentCall && concurrent.Counters.ConnectionOpens == 0);
            cancel.Cancel(); await active; fixture.HoldAt = null; fixture.Hooks.Clear();
            Check("canceled owner releases concurrency", (await kernel.RunPageAsync(fixture.Request(), default)).Page is not null);
        }
        fixture = new();
        foreach (var invalid in new SourcePageRequest?[] { null, fixture.Request(size: 0), fixture.Request(size: 11), fixture.Request(ordinal: 1), fixture.Request(phase: (SourceQueryPhase)99), fixture.Request(phase: SourceQueryPhase.Continuation, ordinal: 1), fixture.Request(continuation: SourceNativeValue.Integer("int", 0)), fixture.Request(limits: new(1, 1, 1, 1, 1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero)), fixture.Request(scope: new(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())) })
            Denied("invalid request no ports", await fixture.Kernel().RunPageAsync(invalid, default), SourcePageReason.InvalidInput);
        fixture = new(); result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        fixture.History = new(SourceHistoryState.Previous, result.Receipt!.OriginalStartedAtUtc, result.Receipt);
        var validContinuation = result.Receipt.NextContinuation;
        foreach (var invalid in new[] { fixture.Request(ordinal: 2, phase: SourceQueryPhase.Continuation, continuation: validContinuation),
            fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: SourceNativeValue.Integer("int", 99)),
            fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: validContinuation, limits: fixture.Limits(timeout: TimeSpan.FromSeconds(20))),
            fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: validContinuation, pair: fixture.MakePair(schemaVersion: "changed")) })
            Denied("history bindings no implicit reset or widening", await fixture.Kernel().RunPageAsync(invalid, default), SourcePageReason.HistoryMismatch);
        fixture.History = new(SourceHistoryState.Unavailable, fixture.Clock.GetUtcNow(), null);
        Denied("untrusted missing history", await fixture.Kernel().RunPageAsync(fixture.Request(ordinal: 1, phase: SourceQueryPhase.Continuation, continuation: validContinuation), default), SourcePageReason.HistoryMismatch);

        fixture = new(); result = await fixture.Kernel().RunPageAsync(fixture.Request(), default);
        var protectedObjects = new object[] { fixture.Scope, fixture.Pair, fixture.Request(), fixture.Request().Identity, fixture.Limits(), fixture.Binding,
            fixture.History, fixture.Schema, fixture.Schema.Fields[0], fixture.Rows[0], fixture.Rows[0].Values[0], result, result.Page!, result.Receipt!, result.Page!.Rows[0],
            result.Page.Rows[0].Fields[0], result.Page.Rows[0].Fields[0].Provenance, result.Page.Rows[0].Fields[0].Provenance.InstalledModules[0],
            result.Conflicts[0], result.Conflicts[0].First, fixture.LastCommand!, fixture.LastCommand!.PageSize, new SourcePermissionReceipt(fixture.Scope, fixture.Pair.PairId, Guid.NewGuid(), "protected-id", "protected-version", new(true, [SourceCapability.MinimumRead])),
            new SourceNativeComparisonReceipt(fixture.Binding, SourceNativePurpose.Paging, 0, SourceNativeOrder.Equal),
            new SourceValueClassificationReceipt(fixture.Binding, 0, FieldDisposition.Included), new SourceAuthorityResolution(SourceAuthorityState.Current, fixture.Binding), new SourcePlannedGap(0, FieldDisposition.Redacted),
            new SourceWarningIdentity(fixture.Scope, fixture.Pair.PairId, 0, Guid.NewGuid(), "protected-id", "protected-version"),
            new SourceWarningReceipt(new(fixture.Scope, fixture.Pair.PairId, 0, Guid.NewGuid(), "protected-id", "protected-version")) };
        foreach (var protectedObject in protectedObjects) Check("protected constant ToString", protectedObject.ToString() == protectedObject.GetType().Name);
        Check("caller cannot invent admitted provenance", typeof(SourceProvenance).GetConstructors().Length == 0 && typeof(SourcePage).GetConstructors().Length == 0 && typeof(SourcePageReceipt).GetConstructors().Length == 0 && typeof(SourceMinimizedRow).GetConstructors().Length == 0);
        Check("no physical provider dependency", !typeof(SourcePageKernel).Assembly.GetReferencedAssemblies().Any(a => a.Name == "Microsoft.Data.SqlClient"));
        return count;

        void Check(string name, bool passed) { if (!passed) { Console.Error.WriteLine(name); throw new InvalidOperationException(); } count++; }
        void Denied(string name, SourcePageResult actual, SourcePageReason expected)
        { Check(name, actual.Page is null && actual.Receipt is null && actual.Reason == expected && actual.Outcome is not SourcePageOutcome.PageReady); }
    }

    private static ScriptedSourcePorts TypedFixture(QueryPackField[] fields, SourceNativeValue[] values)
    {
        var fixture = new ScriptedSourcePorts(); var projection = string.Join(", ", fields.Select(f => f.Name));
        var first = $"SELECT TOP (@PageSize) {projection} FROM fixture.NativeFixture ORDER BY K ASC";
        var continuation = $"SELECT TOP (@PageSize) {projection} FROM fixture.NativeFixture WHERE K > @After ORDER BY K ASC";
        fixture.Descriptor = fixture.Descriptor with { Fields = fields, Sql = continuation, SqlSha256 = ScriptedSourcePorts.Hash(continuation) };
        fixture.Expected = fixture.Expected with { SqlSha256 = ScriptedSourcePorts.Hash(continuation), Policy = fixture.Expected.Policy with { IncludedFields = fields.Select(f => new FieldKey("fixture-category", f.Name)).ToHashSet() } };
        fixture.Pair = fixture.MakePair(firstSql: first, uidField: null);
        fixture.Binding = fixture.MakeBinding(uidField: null); fixture.Schema = fixture.DefaultSchema(); fixture.Rows = [new(0, values)];
        return fixture;
    }
}

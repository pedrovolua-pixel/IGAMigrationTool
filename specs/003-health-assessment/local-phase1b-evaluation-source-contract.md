# Local Phase1B evaluation source capture contract

Status: DRAFT engineering contract — dependent implementation awaits coordinator freeze and nonauthor review  
Date: 2026-10-03  
Scope: Cycle05 capture-only packet; approved Phase1B read boundaries, FR-HAS-11/13/50–53 and bounded AC-HAS-12/19/TP-HAS-012/019.  
Authority: [Phase1B approval](local-phase1b-approval.md), [evaluation procedure](evaluation-plan.md), [cycle04 next dependency](local-evaluation-workflow-verification.md), accepted ADR0001–0004, [paired expectations](local-phase1b-evaluation-source-test-plan.md).

This routine local engineering slice exposes a verified in-memory capture from the existing saved fictional Phase1B run. It is not the source-backed review workflow, sampling manifest, current reviewer/evidence authority integration or live adapter called for by cycle04. It creates no permission, host route, UI, migration, persistence, customer source or acceptance result. The immutable cycle04 120/100 population and contracts remain unchanged.

## Source and identities

`DemoPhase1BCatalog` fixes twelve planned coverage keys: ten deterministic and two AI. The successful normal AI work contains schedule/High and retry/Medium findings, both `OPERATIONS` / `SyntheticOperations`, original AI confidence80/weight1 and Proposed disposition. The two distinct original root-cause groups each affect one object. Source groups and originals, not attempts, current assessment dispositions or deterministic findings, are captured. Existing native SHA256 group/occurrence IDs, uppercase labels, `ev-` references and original rule/proposal/attempt identities remain exact; do not rename them to the evaluation sampler's `synthetic-` grammar. No guessed aliases, confidence bands, extra environment, duplicated runs, padding or invented23 version bindings.

A verified terminal AI gap remains a gap with exact CoverageKey, state, reason and stage. It does not become a generated finding, evaluation member, Pass or denominator entry. A successful saved AI conclusion remains a finding even if the existing assessment review later rejects/defers it. The captured original and the evaluation vote are different records. Desired-outcome locks are provenance only; no desired-outcome quality score or general-AI membership is inferred from them.

## Exact internal API

New module `SyntheticEvaluationSourceIntegration`, using existing Npgsql10.0.3, AssessmentRuns, SyntheticAiExecution, SyntheticOutcomePriority and SyntheticSourceFence. No new package or owning-module SQL.

```csharp
enum Phase1BEvaluationSourceIssue {
    InvalidInput, Denied, NotFound, NotInitialized,
    MigrationDrift, SourceUnavailable, IntegrityMismatch
}
record Phase1BEvaluationSourceResult(
    Phase1BEvaluationSourceIssue? Issue, Phase1BEvaluationSourceCapture? Capture);
sealed class Phase1BEvaluationSourceAdapter {
    Phase1BEvaluationSourceAdapter(SyntheticDurableRunEngine runs,
        SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes);
    Task<Phase1BEvaluationSourceResult> CaptureAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId,
        AiAuthority aiAuthority, OutcomeAuthority outcomeAuthority,
        CancellationToken cancellationToken = default);
}
```

Capture and nested member/gap/occurrence views are detached, get-only immutable classes with internal constructors. Public capture properties are RunId, RunRevision, InputDigest, AiSnapshotDigest, OutcomeLockDigest, AnalysisDigest, ObservedAtDatabaseUtc, Members, Gaps, CanonicalJson and ContentDigest. Member properties: GroupId, AffectedObjectCount, Occurrences. Occurrence properties: OccurrenceId, CoverageKey, OriginalJson, OriginalDigest, GeneratedFindingJson. Gap properties: CoverageKey, State, ReasonCode, Stage. All collections are detached read-only views; strings retain exact saved text. CoverageKey is copied. OriginalJson contains the full unchanged AiFindingOriginal canonical value; GeneratedFindingJson contains the corresponding unchanged SyntheticGeneratedFinding canonical value, including native provenance/recommendation fields. No trusted object can be replaced through a returned view.

## Transaction, authority and integrity

The caller supplies an already open compatible transaction, owns commit/rollback/disposal and must enter before taking other row/budget locks. Validate non-null dependencies/input, nonempty UUID, transaction.Connection identity and open connection before any source access. Require the existing Phase1B loopback guard: host127.0.0.1/localhost, port55433, useriga_synthetic, database prefixiga_synthetic_phase1b_. The owning run engine independently verifies its configured connection identity. No new connection or transaction is opened; the adapter never commits, rolls back, initializes schemas, seeds reviews or dispatches providers.

Before reading protected source, require the existing `AiExecutionPolicy.Authorize(..., Read)` and `OutcomePriorityPolicy.Authorize(..., ReadOutcome)` decisions with exact fixed scope, active nonrevoked assignment/resource and applicable categories. Both trusted authorities must identify the same actor and each must carry exactly its respective Consultant role (AiRole.Consultant / OutcomeRole.Consultant); mixed Worker/Consultant or CustomerOutcomeApprover/Consultant authority is denied even if the individual owner permits its separate action. No actor/grant/category is derived from HTTP/model/body data. This bounded Consultant composition grants nothing beyond each existing policy. Existing Consultant content-read access is preserved; an Auditor export or QualifiedReviewer role is not elevated. The in-memory full AI snapshot includes fictional packet/proposal content already permitted by the owning Consultant Read; capture grants no export, transmission, downstream serving or storage permission. A caller allowed by only one owner is denied. No actual human independence or review eligibility is asserted.

Acquire existing outcome-registry fence, then existing run source fence, using fixed source scope. Read through `runs.ReadInTransactionAsync`, `outcomes.ReadLockedAsync`, `ai.ReadInTransactionAsync` on that exact connection/transaction. Each owner verifies its own schema, provenance and current authority; AI counter/budget verification may take locks, including FOR UPDATE. Therefore this is non-mutating capture, not a PostgreSQL READ ONLY transaction. No direct cross-module SQL, `DemoPhase1BService.Core` call, review seeding, fallback fixture generation, independent-latest join, export/provider reconstruction or health scoring.

Require `DemoPhase1BCatalog.MatchesFrozenFixture`, Scoring state, no cancellation and complete reconciled coverage. Use existing `SyntheticPhase1BAnalysisAdapter.Originals(run, ai, locked)` to verify the compound source and build its unchanged originals. Missing/stale/mixed/incomplete proofs return SourceUnavailable or IntegrityMismatch as specified below; never substitute a generated seed or partial capture. For the accepted fixed profile, exactly two saved AI terminal outcomes must match the planned keys. Capture each generated AI group once and retain every member occurrence/object; verify occurrences map one-to-one to saved AI originals and that every saved finding belongs to one captured group. Exclude deterministic groups. A group mixing detection methods or incompatible originating metadata denies IntegrityMismatch rather than choosing a primary mapping.

The source fence proves one coherent Phase1B database observation until the caller releases it. It does not fence another database, establish current authorization after capture, or make the returned bytes an authorization token. A later consumer must obtain its own exact approved current-source/authority contract before serving protected data or accepting reviews. Ordinary capture makes no delivery or persistence claim.

## Canonical envelope and errors

The compact UTF8 canonical envelope uses camelCase properties recursively sorted ordinal, default System.Text.Json escaping, named enum strings, explicit nulls, no whitespace/BOM/newline and SHA256 lower hex of exact bytes. Arrays have semantic order: members ordinal GroupId, occurrences ordinal OccurrenceId, gaps ordinal EvidenceCategory then InventoryId. Embedded source JSON values are strings retaining owning canonical bytes: AI locks/snapshot/originals use AiExecutionCanonical.Serialize, outcome locks use OutcomePriorityCanonical.Json. GeneratedFindingJson uses the unchanged SyntheticCanonicalDigest representation (default serializer property names/numeric enums, recursively ordinal property ordering); the adapter materializes those bytes because that owner exposes only Compute, and does not change its digest contract. Non-owning frozen run-input JSON and coverage data use the top-envelope declared canonical encoder. No numerical confidence adjustment or derived accuracy is emitted.

Envelope contains exactly: schemaVersion=`synthetic-phase1b-evaluation-source-v1`; scope (customerId/projectId/environmentId); runId; runRevision; checkpointSequence; inputDigest; baselineId; profileId; runState; cancelRequested; runUpdatedAtUtc; observedAtDatabaseUtc; frozenInputsJson; aiRunLockJson; aiSnapshotJson; aiSnapshotDigest; lockedOutcomeSetJson; outcomeLockDigest; analysisDigest; members; gaps. Members contain groupId, affectedObjectCount and occurrences (occurrenceId, key, originalJson, originalDigest, generatedFindingJson). Gaps contain key, state, reasonCode and stage. Scope/key fields use existing native field names. Full AI snapshot canonical JSON binds original mapping, attempts, budget/counter provenance and coverage, while outcome-lock canonical JSON binds exact approved selections/history. The capture digest binds every emitted semantic field, including observation time. Two separate database observations can have different hashes because observation time differs; there is no claimed stable repeated-read snapshot ID. Captured inputs serialized twice have identical bytes.

Error mapping: malformed connection/transaction/UUID or mismatched configured run connection => InvalidInput; wrong scope/current owner authorization/actor mismatch => Denied; absent run or owning bound run => NotFound; missing initialized owner => NotInitialized; schema drift => MigrationDrift; nonfixed profile, non-Scoring/cancelled/incomplete source or owning SourceUnavailable/InvalidState/SourceConflict => SourceUnavailable; other ownership integrity/input-proof failure or failed compound/group verification => IntegrityMismatch. No failure exposes source content, IDs, counts, digests, SQL or exception text: Issue is closed and Capture null. Cancellation remains normal CancellationToken propagation. Infrastructure failures may propagate to the caller without a payload result; this internal library adds no logging or HTTP exception handling.

## Compatibility, lifecycle and verification

Capture retains an in-memory observation only; caller disposes source transaction. No persistent uniqueness/predecessor, live revocation, two-environment independence, retention/deletion, publication or new deployment setting. Historical module/fixture/migration bytes and cycle04 population/sample hashes remain unchanged. Disable the new consumer/library reference for rollback; no data repair/reset/down migration. Coordinator owns solution/project/shared CI configuration, canonical cycle records and private site. Author owns this module and focused tests after freeze. Independent tests must exercise real owning PostgreSQL reads, nonmutation, authorization, complete originals/gaps, source-tamper denial and lock release/concurrency. Full source-backed M08/Phase1C acceptance and live gates remain open.

No consequential architecture/security choice is resolved here: sampler aliases, real23bindings, source-backed saved review semantics and current evidence/reviewer authority remain outside this packet. Review the paired expectations and freeze this engineering contract before writing runtime.

# Independent authority and audited session composition

Local synthetic verification of the approved P01–P05 packet. This harness is authored by a reviewer who did not author the authority or audit implementation. It composes the real PostgreSQL authority admission policy with the strict audited ticket store; it never uses an always-eligible policy.

## Run

Use a dedicated loopback PostgreSQL cluster and database named `iga_synthetic_bff_composition`. Set `IGA_COMPOSITION_TEST_CONNECTION` to the fixture operator connection. The operator installs migrations and restricted local roles. Actual behavioral operations connect separately as nonsuperuser administrator, provider and runtime LOGIN roles; function ownership belongs to a distinct NONLOGIN role. These are disposable local fixture grants, not an Azure or production permission template.

```sh
dotnet run --project tests/integration/BffAuthorityComposition.Tests -- --reset-synthetic-schema
```

The command deliberately resets only its dedicated database schemas. It refuses other database names, non-loopback hosts and missing explicit reset intent. No Graph, Entra, Azure, or external HTTP calls occur.

## Acceptance mapping

| Packet | Independent combined checks |
| --- | --- |
| P01 | Attributed pending/active enrollment and exact assignment; receipt before expected revision; conflicting digest; simultaneous retries/new-subject race; exact scope/category; subject mutation invalidates old sessions while preserving other subjects. |
| P02 | Real authority admission on ticket issuance, retrieval, renewal and rotation; resource/home cutoff versus original authentication; external Member lifecycle and home proof; expired provider/assignment after audit-head contention. |
| P03 | Authority/ticket/event/receipt atomic rollback; direct restricted SQL bypass denial; canonical event/request/receipt byte checks with recomputed digests; standalone mutation-event receipt constraint; writer isolation between known streams; metadata-only receipt reconciliation after deterministic post-COMMIT acknowledgment loss; independent ordered chain and receipt coverage. |
| P04 | Covered by separate actual HTTPS hosting harness. This project does not substitute for its peer/host/key checks. |
| P05 | Independent local execution and source review. Applicable repository gates and owner approval remain coordinator responsibilities. |

## Evidence limits

Final source: AUDIT `64716f6` + `d9173e9` + `970a1ab`; AUTH `1e5ecb4` + `0a7da148`; BFF bridge `4202ab8`. The independently authored and executed combined fixture passed 258 assertions in the dedicated reviewer-owned PostgreSQL 18.4 database. Locked audited restore, project formatting verification, scoped secret scan, and Release build passed with zero warnings/errors. Independent execution of the frozen author's separate audit corpus passed 81 additional restricted-role/lifecycle/tombstone/witness checks in another reviewer-owned database; those 81 cases were not authored by this reviewer.

No remaining material blocker for the approved local implementation was found after the required source corrections. These results do not grant human acceptance or production approval. Shared solution/CI configuration and the complete repository gate matrix are coordinator-owned.

The fixture deliberately throws a timeout after an actual successful COMMIT, then looks up the internal receipt and retries the exact command. This proves metadata-only reconciliation without another version/event or credential replay; it does not prove a real network failure during COMMIT. Live provider adapters, Azure bindings and permissions, independently retained audit witness, operational outage preservation, retention jobs, backup restore, and production activation are not verified here.

The initial authored fixture attempts found an await in a nonasync lambda and a parameterized multi-statement PostgreSQL setup error; both were corrected before the passing run. The separate hosting fixture initially failed clean locked restore because its normal SDK project lacked an explicit ASP.NET framework reference; coordinator `4dbb784` corrected it. Independent hosting clean restore/build/format and all 60 actual HTTPS/framework data-protection checks passed after that correction.

Local cluster initialization required an approved sandbox escalation for PostgreSQL shared memory. A port collision during setup created only an empty reviewer fixture database on the author's local cluster. Initial cleanup approval was rejected without ownership/emptiness evidence; read-only proof and author consent then permitted deletion of that exact empty database. The author's database/process was preserved. All executed composition/audit checks used only the reviewer-owned cluster on loopback port 55448.

## Synthetic timestamp precision regression

The fixture initializes and resamples its frozen wall clock at PostgreSQL's microsecond timestamp precision. PostgreSQL JSON text casts can round a seven-digit fractional timestamp up, while .NET retains 100 ns ticks. With a frozen clock ending in nine submicrosecond ticks, real provider publication stored `provider_checked_at` one tick ahead of that clock; the unchanged future-provider admission guard correctly rejected initial session issuance. The pre-repair failure was reproduced independently of CI.

The regression exercises all nine nonzero submicrosecond remainders through actual provider publication, verifies stored timestamp precision, proves the nine-tick future case, and checks current-version issuance remains denied for every rounded-future observation. Separately enrolled subjects at monotonically advanced microsecond boundaries prove successful issuance and retrieval across two stores. It also verifies resampled wall-clock precision; existing expiry, cutoff and contention denials remain present. No production timestamp comparison, schema, retry, dependency or permission policy changes.

The corrected fixture passed 644 assertions before formatting and 643 on the formatted rerun on worker-owned disposable PostgreSQL 18.4. The count varies with the half-microsecond rounding case, which adds a denial assertion only when PostgreSQL rounds forward. Both counts include the original combined scenarios plus the precision cases and their additional records in the independent audit-chain/receipt checks. It does not supersede the live-provider and production evidence limits above.

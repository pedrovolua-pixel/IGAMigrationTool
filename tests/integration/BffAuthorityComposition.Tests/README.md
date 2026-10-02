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

Compiled checkpoint: AUDIT `64716f6`, AUTH `1e5ecb4`, and BFF bridge `4202ab8`. The independently executed fixture passed 252 assertions in the dedicated reviewer-owned PostgreSQL18.4 database. Locked audited restore and Release build passed with zero warnings/errors. Required subsequent author corrections still need integration and a final rerun; this checkpoint is not final review completion.

The fixture deliberately throws a timeout after an actual successful COMMIT, then looks up the internal receipt and retries the exact command. This proves metadata-only reconciliation without another version/event or credential replay; it does not prove a real network failure during COMMIT. Live provider adapters, Azure bindings and permissions, independently retained audit witness, operational outage preservation, retention jobs, backup restore, and production activation are not verified here.

The initial authored fixture attempts found an await in a nonasync lambda and a parameterized multi-statement PostgreSQL setup error; both were corrected before the passing run. The separate hosting fixture initially failed clean locked restore because its normal SDK project lacked an explicit ASP.NET framework reference; coordinator `4dbb784` corrected it. Independent hosting clean restore/build/format and all60 actual HTTPS/framework data-protection checks passed after that correction.

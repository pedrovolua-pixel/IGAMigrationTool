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

Execution results and immutable source commits will be recorded after author checkpoints are integrated. A discarded successful response followed by receipt lookup represents simulated acknowledgment loss; it does not prove a real network failure during COMMIT. Live provider adapters, Azure bindings and permissions, independently retained audit witness, operational outage preservation, retention jobs, backup restore, and production activation are not verified here.

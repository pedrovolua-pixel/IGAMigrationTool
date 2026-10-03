# Local evaluation workflow scope receipt

Status: AUTHORIZED bounded local implementation; engineering freeze required before dependent code
Date: 2026-10-03
Owner: Phase1C coordinator

The repository owner instructed “Do it” after the recommendation to connect the approved sampling and reviewer policies to local assignments, recorded reviews/corrections and immutable history, beginning with exact storage/API/UI contracts and test expectations. This receipt records that instruction without changing the historical [EV02 approval receipt](local-evaluation-policy-approval.md).

The scope is the independent fictional evaluation workflow in [cycle04](../../plans/active/local-pilot-m08-evaluation-workflow-cycle04.md). A dedicated guarded PostgreSQL store, explicitly enabled loopback ASP.NET host and dedicated React workspace implement the approved policies within accepted ADR0001–0004. They do not create production activation, new roles/grants, assignment administration, a raw evidence resolver, adjudication or retention rules. The same server-seeded reviewer and two exact environment assignments are retained. Trusted coordinator orchestration captures each accepted outcome version automatically; the reviewer gains no manual freeze or publication permission.

The source/admitted cohort remains the canonical120/100-member synthetic fixture. Registry and review updates never trigger resampling. Outcome versions bind the preserved source/sample locks, actual registry/event chain, their own cutoff and immutable predecessor. Registry state and reviews share one transaction/aggregate lock. Replay rechecks current authorization before returning an original actor-bound metadata receipt.

Phase1B integration is under development by another owner. This independent slice neither depends on that work nor claims a live adapter or cross-database source fence. Existing hosts, application entry, source contracts and unrelated work are outside ownership. Real assignments, queue/adjudication, evidence lifecycle, production identity, report publication, customer acceptance, fullM08/Phase1C/TP-HAS-012/019 and G1–G9 remain unverified.

The nonauthor authority audit confirmed this scope is routine engineering under the existing approved product and policies. Closed engineering contracts, test expectations and nonauthor review are still required before dependent implementation. Engineering review is not human product/security/operations acceptance.

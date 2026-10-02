# Fictional fix-package preview

This framework-only, standalone module consumes a complete synthetic `RecommendationGuidance.GuidanceSnapshot`, rebuilds it through the upstream validator, and binds three fixed generic fictional templates to each existing option. One package preserves each existing finding group. All artifacts remain `Unverified`, regardless of finding state. These examples establish no supported One Identity remediation, approval or recovery result.

The [exact internal contract](../../../../docs/development/cycle11-fictional-fix-package-contract.md) defines canonical UTF-8 bytes, complete source/template digests, stable identities, ordering, bounds and warnings. Recomputed guidance integrity is a fictional test-host boundary; it does not authorize customer input or validate customer facts. Existing guidance and all application profiles, saved runs, scoring, review and draft contracts remain unchanged.

`FixPackageBuilder.Build(guidance)` returns a payload-free issue or an immutable snapshot. `FixPackageHtmlRenderer.Render(snapshot)` revalidates actual fields and produces framework-encoded, deterministic HTML with fixed restrictive CSP and native disclosure. Identifiers, prose, references and code remain text; there are no supplied URLs, resource loads, execution, review, task, export or download controls. Upstream unavailable statements are shown explicitly as the historical guidance boundary; the new preview disclaimer describes this separate layer.

Run the portable fixture host with the pinned repository SDK:

```sh
dotnet run --project tests/integration/SyntheticFixPackages.Tests --configuration Release -- --write-previews /tmp/iga-fix-previews
```

It produces fixed normal, hostile-content and empty fixtures plus independent expected browser metadata. The actual browser verifier reuses the pinned Playwright package from the existing consultant test project:

```sh
node tests/e2e/synthetic-fix-packages/verify.mjs --preview-directory /tmp/iga-fix-previews --playwright-module /absolute/path/to/tests/e2e/consultant-demo/node_modules/playwright/index.mjs
```

Use a separate fixture-only process with an explicit minimal environment, excluding application/database/provider credentials. The browser harness serves only the fixed fixture allowlist on ephemeral loopback, denies other requests, and blocks external fetches. It checks complete provenance/display, hostile text, absence of actions/resources, keyboard disclosure and narrow-screen reflow. This bounded local proof does **not** verify the deployed ADR-0001 worker sandbox, customer security, supported manual/browser/accessibility matrix or full TP-HAS-018 (task/CSV cases remain absent).

There is no new dependency, migration, configuration, application endpoint, input profile, actual provider, protected resolver or durable artifact history. Consultant review/invalidation, objectives, priority/effort, tasks/CSV, execution, validated remediation and report publication remain later contracts. See [Cycle11](../../../../plans/active/local-pilot-fix-packages-cycle-11.md) for execution and review state; completion never grants milestone/gate acceptance.

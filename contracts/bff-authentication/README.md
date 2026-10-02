# BFF authentication v1

The repository owner approved exact D01/D02 on 2026-10-02 in [the decision record](../../docs/development/bff-production-contract-proposal.md). This OpenAPI 3.1 artifact freezes the three local authentication operations, strict body limits, fixed completion path, status precedence and minimal JSON. Framework callbacks are separately owned by supported OIDC validation. Health endpoints retain their existing contract.

Generate/check unused TypeScript declarations with `node contracts/bff-authentication/generate-types.mjs [--check]`. The reusable browser helper gets the token and submits a native hidden form. It is not connected to the synthetic consultant UI. The server validates Origin, UTF-8 form and synchronizer fields; it does not claim to prove navigation mode from undeclared Fetch Metadata headers. A caller must follow the approved normal-navigation contract.

Only an explicit synthetic HTTPS composition enables the library in tests. The diagnostic executable remains permanently disabled. Production enrollment, provider app-role/guest-origin/cutoff retrieval, privileged CA proof, shared keys/proxy/audit composition, image acceptance and deployed activation remain separate reviewed dependencies. Missing evidence denies. No deployment or migration is performed by these files.

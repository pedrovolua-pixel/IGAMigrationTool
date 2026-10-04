# Portable population readiness checks

Run with the repository pinned SDK: `dotnet run --project tests/unit/SyntheticEvaluationPopulationIntegration.Tests -c Release`.

No database or provider is used. Executed checks cover independent preimplementation framed reference vectors, strict UTF8/case/whitespace/Unicode/length/order boundaries, exact canonical encoder and UTC/decimal recipes, the independently authored 1327-byte full-envelope literal and SHA256, immutable detached collections and closed public types, exact binding kind order, native string retention and observation hash binding. The full-envelope literal is test data copied to output; runtime code never reads repository specification files.

These checks do not establish qualified review, complete sampling or live gates. A real SHA256 collision branch is not fabricated.

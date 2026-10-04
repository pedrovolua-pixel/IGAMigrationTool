# Closed authority and synthetic provider tests

Build and execute `IdentityAuthority.Tests.dll` using the repository-pinned .NET SDK. This dependency-free executable covers P01/P02 record and parser invariants, canonical full-command digests, exact scope/category/condition projections, strict enum/GUID/UTC input, and a bounded direct-role paging seam.

The provider uses fictional bytes and time only. Missing/typed/duplicate/extra data, wrong subjects/resources/roles, group or default roles, hostile paging, provider errors, conservative expiry, external Member lifecycle and absent/mismatched/stale home evidence deny. No real endpoint or provider permission is exercised. The PostgreSQL executable separately proves transaction and restricted-role boundaries.

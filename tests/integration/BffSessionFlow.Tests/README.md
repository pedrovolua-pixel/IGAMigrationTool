# BFF session transport integration

Synthetic-only executable. It starts two HTTPS Kestrel listeners on loopback with a generated disposable test certificate, a shared temporary Data Protection key ring, and a separate local PostgreSQL database with both explicit additive identity-session migrations. No production sign-in endpoint or identity bypass is added to product code. Fixtures seed server sessions through ASP.NET authentication services outside HTTP routes; they do not simulate successful Entra authentication or managed-identity redemption.

The test refuses a non-loopback database or any database name outside `iga_synthetic_bff_`. Set `IGA_BFF_FLOW_DATABASE` to the disposable database connection. Run with the pinned SDK after locked restore and Release build. Tested operations and exact outcomes belong in the cycle evidence record.

Coverage: real opaque cookie serialization, shared-store authentication on another server, synchronizer CSRF checks for mutations, authoritative product policy with identifier substitution, logout/individual/subject revocation, fail-closed provider status and session deadlines. Full OIDC signature/nonce/state/PKCE/metadata/key rollover and deployed federation remain NOT VERIFIED by this executable.

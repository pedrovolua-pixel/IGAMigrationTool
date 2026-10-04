# Public BFF authentication transport verification

Synthetic actual HTTPS Kestrel checks for accepted D01, using the supported cookie handler,
a deliberately in-memory ticket store and an intercepted supported OIDC redirect event.
No provider, Graph, customer, Azure or product UI request runs. Store removal failure and
same-subject distinct-session CSRF isolation are explicitly exercised. This is transport
verification; production provider claims, shared production keys, proxy and audit integration
remain unverified. The diagnostic executable does not install these reusable routes.

Run from the repository root with the pinned SDK:

```
dotnet run --project tests/integration/BffAuthenticationTransport.Tests --no-restore
```

The server enforces the accepted Origin, bounded form and synchronizer contract. HTTP does not prove browser top-level navigation without additional Fetch Metadata requirements; none are invented here. Browser callers must use ordinary form navigation.

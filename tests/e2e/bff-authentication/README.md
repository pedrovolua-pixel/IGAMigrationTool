# Synthetic BFF native form proof

Run after the Release solution build and installation of the existing pinned `tests/e2e/consultant-demo` Playwright/Chromium dependencies: `IGA_DOTNET=<pinned dotnet> node tests/e2e/bff-authentication/verify.mjs`.

This compiles the exact unused repository navigation helper, starts the synthetic actual-HTTPS transport fixture on an ephemeral loopback port, gets its CSRF token in Chromium and proves a native document form POST. The fixture's provider redirect is intercepted before any network connection. It verifies the secure HttpOnly narrow CSRF cookie and singleton form field. It does not exercise real Entra/CA/federation, production identity, deployed callbacks, accessibility acceptance or an activated product UI.

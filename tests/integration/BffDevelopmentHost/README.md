# Disabled BFF package verification

Only synthetic GUIDs and a noncredential disposable-looking database descriptor are used. No Azure or real provider/database account is required. Run with pinned .NET SDK 10.0.401:

```sh
dotnet restore src/server/hosts/BffDevelopmentHost/BffDevelopmentHost.csproj --locked-mode
dotnet build src/server/hosts/BffDevelopmentHost/BffDevelopmentHost.csproj -c Release --no-restore
python3 tests/integration/BffDevelopmentHost/verify_inputs.py
python3 tests/integration/BffDevelopmentHost/verify.py --dotnet /absolute/path/to/pinned/dotnet --assembly src/server/hosts/BffDevelopmentHost/bin/Release/net10.0/BffDevelopmentHost.dll
```

The process test requires free loopback port 8080 and bind permission. It starts and stops only its own process, SQL/proxy connection traps and temporary synthetic settings directory. It exercises real invalid startup/configuration/activation refusals, fixed health contract, raw-path/method substitution, disabled callbacks with spoofed HTTPS headers, bogus cookies, absent cookie/redirect issuance, no SQL/proxy traffic, no working-directory key persistence, ignored file/listener overrides and graceful shutdown with no protected logs. The rejecting store/provider implementations guarantee no ordinary operation can call their composed network dependency. Proxy traps alone are not proof of arbitrary network isolation; actual Azure boundaries remain unverified.

On a Docker-capable Linux AMD64 runner:

```sh
python3 infra/containers/build-bff-development.py
python3 tests/integration/BffDevelopmentHost/verify.py --docker-image iga-bff-development:local
```

The actual container check enforces UID 1654, read-only root, dropped capabilities, no new privileges, loopback-only diagnostic port and bounded resources; then repeats real request refusals and confirms graceful SIGTERM/no logs. Docker smoke is NOT VERIFIED when no engine is available; input inspection does not substitute. Image scan/license acceptance, restricted signed evidence, production host/proxy/keys/adapters/CA and Azure sign-in remain separate gates.

## Worker execution — 2026-10-02

Pinned restore (including audited dependency resolution and subsequent `--locked-mode`), scoped formatting and `--verify-no-changes`, and Release build passed with zero warnings/errors. The actual-process harness passed 254 executed assertions, including 12 startup/configuration refusal cases and zero SQL/proxy trap connections. The exact context/base-lock/local-tag checker passed. No local Docker engine is available, so actual Linux container build/run remains NOT VERIFIED for coordinator CI. These results concern only the permanently disabled diagnostic executable, not production authentication or an Azure gate. Initial sandbox package restores stalled; only the identified worker restore processes were stopped before the authorized package-network/compiler-IPC retry. No cloud operation or public push occurred.

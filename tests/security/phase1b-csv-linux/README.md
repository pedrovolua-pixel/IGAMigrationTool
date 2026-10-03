# Phase 1B CSV Linux boundary smoke

This is approved synthetic verification for CSV1B-T06, not a production renderer or G5 acceptance. It tests the existing pure renderer and a separate adversarial probe linked to the same pure CSV codec. The probe is never run with negative modes on the host. The runner runs native benign exact-byte checks before Linux checks; native success cannot stand in for container isolation.

Prerequisites: Python 3, pinned .NET SDK `10.0.401`, Docker with a Linux daemon, and an **already locally loaded** Linux arm64/amd64 .NET runtime image. The default runtime is `mcr.microsoft.com/dotnet/runtime:10.0.12`, matching the pinned SDK's installed runtime. CI ownership includes provisioning/preloading that image and wiring this runner. This runner never installs software, pulls an image, changes daemon configuration, or requests credentials.

From the repository root:

```sh
python3 tests/security/phase1b-csv-linux/run.py --evidence /tmp/phase1b-csv-linux-verification.json
```

For an isolated checkout without the CSV project, point at the coordinated source:

```sh
Phase1BDotnet=/private/tmp/iga-dotnet-10.0.401/dotnet \
Phase1BRendererProject=/private/tmp/iga-phase1b-coordinator/src/server/workers/SyntheticCsvRenderer/SyntheticCsvRenderer.csproj \
python3 tests/security/phase1b-csv-linux/run.py --evidence /tmp/phase1b-csv-linux-verification.json
```

`Phase1BRuntimeImage` can select another **preloaded** approved runtime image; evidence binds its actual image ID and repository digests. The default `Phase1BRendererProject` is the portable relative repository path. The probe's `Phase1BCsvProject` override is derived from that source root. `--check-only` intentionally executes only native benign parity. Exit `0` means the requested checks passed, `1` means failure, and `77` means native parity passed but Linux did not execute because Docker was absent. CI must require exit `0` **and** `linuxStatus == "PASS"` with 18 cases; it must never treat `77` as a Linux success.

The packaging context contains only four publish files per program and this Dockerfile. It contains no repository, configuration, environment files, credentials, or customer inputs. Each container receives a fixed synthetic envelope via stdin and bounded stdout/stderr. There are no host mounts or Docker socket. Image environment is cleared with `env -i`; the five explicit runtime variables disable diagnostics, select invariant globalization, set a 256 MiB managed heap, and choose nonexistent home/temp paths. User/effective UID is `10001`; root filesystem is read-only, networking and IPC are disabled, capabilities are dropped, and `no-new-privileges` is enabled. A test-only seccomp profile denies sockets and process creation while permitting .NET threads; `clone3` returns ENOSYS so libc can use thread-only `clone`. A fictional unreadable marker proves a real denied read. It is not a protected/customer file.

Every run inspects effective Docker user, mount absence, rootfs, network, IPC, capabilities, security options and resource limits before attaching. Limits are the approved 1 MiB input, 4 MiB output, 16 KiB stderr and 10-second wall bound, plus bounded verification limits: one CPU quota, 2/3-second soft/hard CPU rlimit, 384 MiB cgroup memory with equal memory+swap ceiling, 256 MiB managed heap and 32 pids. No writable tmpfs is provided. Log driver `none` prevents daemon output accumulation. The parent caps streams, kills only its container on violations, and removes only its own containers and random local image tags. Native publication strips PDB/unrelated output to the fixed closure. Locked restore adds no packages.

The 18 Linux cases are renderer one-row/header-only byte parity, invalid/oversized input rejection, actual effective UID/environment checks, socket denial, app/temp write denial, unreadable marker denial, process creation denial, wall/stdout/stderr parent cutoffs, CPU termination, managed-memory rejection, cgroup OOM kill and pid exhaustion. A denial probe succeeds only if it then renders the exact independent golden bytes; generic startup failure cannot pass. Resource probes require their specific cutoff/exit/OOM outcome. No negative probe output is delivered as CSV. Evidence records counts/digests, source files, sealed binaries, runtime image identity and effective bounds, without raw envelope, stderr, secrets or host environment values.

`native-verification.json` records executed local evidence. On the author's Mac Docker CLI was absent: four native benign byte comparisons passed; **zero Linux cases executed**. Linux denial/resource assertions remain pending actual CI execution. The default synthetic Linux verification is independent from the production security boundary and release approval.

# Independent local artifact-review verification

This V13 packet independently freezes the approved AR13 source, template, identity, state and replay expectations before inspecting A13/B13 implementation. `preauthor/packet.json` retains original SHA256 `6cc8c175d66a024556c5438565ca7f66aa46d195cb77dfc2d643dba35e6bd581`; the original review receipt remains unchanged. `technical-supplement.json` records the later exact engineering recipes separately.

Run from the repository root:

```sh
python3 tests/integration/LocalArtifactReview.Tests/oracle.py
dotnet restore tests/integration/LocalArtifactReview.Tests/LocalArtifactReview.Tests.csproj --locked-mode --disable-build-servers -m:1
dotnet build tests/integration/LocalArtifactReview.Tests/LocalArtifactReview.Tests.csproj --no-restore -c Release --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet tests/integration/LocalArtifactReview.Tests/bin/Release/net10.0/LocalArtifactReview.IntegrationTests.dll --portable
dotnet tests/integration/LocalArtifactReview.Tests/bin/Release/net10.0/LocalArtifactReview.IntegrationTests.dll
```

No argument runs portable and PostgreSQL cases. `--host-schema-denial <lowercase-D-run-UUID>` is a browser-owned helper for a current saved new-profile run on fixed owned localhost5183; it requires original event trigger O, tests pre-capture503/no-source, and restores it in finally. The browser always invokes separately guarded `--restore-host-schema` afterward, including after helper termination. Neither helper initializes another schema or changes another trigger. `--portable` excludes PostgreSQL. `IGA_ARTIFACT_REVIEW_TEST_DATABASE` must resolve exactly to loopback127.0.0.1:55433, role `iga_synthetic`, dedicated database `iga_synthetic_cycle13_v13_v2`. It creates only that named database if absent. Never restart the cluster, modify other databases or replace earlier immutable fingerprints. First-generation database `iga_synthetic_cycle13_v13` remains untouched after the corrected fingerprint necessitated a fresh v2.

The portable composition uses actual GuidanceBuilder → FixBuilder → SourceBuilder, comparing entire independently authored guidance/package/binding/artifact bytes. Reflection appears only in malformed snapshot rejection cases. Python/JavaScript/C# expected recipes import no production canonical helpers. Observed dynamic saved captures in ignored Release output are execution evidence and are never expected fixtures.

PostgreSQL cases cover trusted authority, exact original reasons, latest-event state, withdrawal, historical actor-bound replay, scoped identity, no writes on read/denial, atomic rollback/cancellation/reconnect, cross-artifact high-water guards, concurrent revisions, saved source/finding changes, actual shared source-fence waits and guarded schema/append-only checks with finally restoration. Browser cases cover actual host/UI commands, source/history/original parity, retries/races, controls/Unicode, historical profiles, keyboard/reflow/axe subset and an owned process restart.

At this source checkpoint, independently executed Python33-file and JavaScript20-check oracles PASS; first source generation619555a had portable218/combined PostgreSQL908 PASS and Release0warnings/errors. Its exact original sources, Release closure and transcripts remain in ignored `.native/first-success`. The initial sandbox restore stalled and was stopped by exact owned PID; the locked audited retry with network/IPC completed. Final formatted combined source passed portable218 and dedicated v2 PostgreSQL911, including all three source-fence writers. Actual browser generation608 failed at a double-scoped root-dl test selector; corrected generation9132 passed state/replay/source/race/recovery/host schema denial and historical boundaries, then failed at keyboard setup because the successful command had cleared its reason and disabled the button. Exact failed source/report/logs and consumed Release files are preserved in ignored `.host`. The final keyboard/viewport/axe replay is PENDING. These counts do not relabel earlier908. This checkpoint is reviewable source, not final feature acceptance.

Windows, supported manual accessibility, production identity, deployed rendering sandbox, UAT/Milestone7 and G1–G9 remain NOT VERIFIED. An explicit temporary `DOTNET_hostBuilder__reloadConfigOnChange=false` host run does not verify default macOS startup. No customer/provider/execution/export authority is granted.

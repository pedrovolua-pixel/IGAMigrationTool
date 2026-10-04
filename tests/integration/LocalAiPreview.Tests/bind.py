"""Bind the actual isolated V10 worker source, executed outputs and safe evidence."""
from pathlib import Path
import hashlib
import json
import subprocess

root = Path(__file__).resolve().parents[3]
integration = root / "tests/integration/LocalAiPreview.Tests"
browser = root / "tests/e2e/local-ai-preview"


def binding(path):
    raw = path.read_bytes()
    return {"path": str(path.relative_to(root)) if path.is_relative_to(root) else str(path),
            "sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw)}


common = [root / name for name in ("global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "IgaMigrationTool.slnx", ".gitattributes") if (root / name).is_file()]
for folder in (root / "src/server/modules", root / "src/server/hosts/LocalConsultantDemo", root / "migrations"):
    for path in folder.rglob("*"):
        if path.is_file() and not {"bin", "obj"}.intersection(path.relative_to(folder).parts) and path.suffix in (".cs", ".csproj", ".json", ".sql"):
            common.append(path)
for path in (root / "src/web").rglob("*"):
    if path.is_file() and not {"node_modules", "dist"}.intersection(path.relative_to(root / "src/web").parts) and path.suffix in (".tsx", ".ts", ".css", ".json", ".html"):
        common.append(path)

original = Path("/private/tmp/iga-cycle09-coordinator/src/server/modules/AssessmentRuns/bin/Release/net10.0/AssessmentRuns.dll")
legacy = json.loads((integration / "legacy-goldens.json").read_text())
assert binding(original)["sha256"] == legacy["assemblySha256"]
head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()

original_root = original.parents[7]
original_head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=original_root, text=True).strip()
assert original_head == "3af83bbea71a59d9c0bde6a0847f5b83bb26f2de"
assert not subprocess.check_output(["git", "status", "--porcelain"], cwd=original_root, text=True).strip()
original_inputs = [original]
for module in ("AssessmentRuns", "AssessmentOrchestration", "AssessmentMaturity", "AssessmentCoverage", "DeterministicAnalysis", "AssessmentScoring"):
    for path in (original_root / "src/server/modules" / module).rglob("*"):
        if path.is_file() and not {"bin", "obj"}.intersection(path.parts) and path.suffix in (".cs", ".csproj", ".json"):
            original_inputs.append(path)
original_inputs += list(original.parent.glob("*.dll"))
integration_paths = common + original_inputs
for path in integration.rglob("*"):
    if path.is_file() and "obj" not in path.relative_to(integration).parts and path.name != "execution.json":
        if "bin" not in path.relative_to(integration).parts or path.suffix in (".dll", ".json"):
            integration_paths.append(path)

browser_paths = common[:]
for folder in (browser, root / "src/web/dist", root / "src/server/hosts/LocalConsultantDemo/bin/Release/net10.0"):
    for path in folder.rglob("*"):
        if path.is_file() and path.name != "artifacts.json":
            browser_paths.append(path)


def unique(paths):
    return [binding(path) for path in sorted(set(paths), key=str)]


integration_meta = {"status": "PASS", "sourceHeadBeforeEvidenceSeal": head,
 "checks": {"portable": 78, "postgresIncludingPortable": 282, "lockedRestore": "PASS", "releaseBuild": "PASS, zero warnings/errors", "ownedFormat": "PASS", "scopedSecrets": "PASS"},
 "database": "owned iga_synthetic_v10 on existing loopback PostgreSQL18.4; rows/triggers restored and fingerprint verified; cluster unchanged",
 "originalOracle": {"preservedPreCycle10AssemblySha256": legacy["assemblySha256"], "originalSourceCommit": original_head, "originalSourceTrackedClean": True, "sixOriginalFactoriesExecuted": True, "currentImplementationProducesExpectedOutputs": False},
 "review": "A10/B10/coordinator actual source inspected independently; no remaining product/runtime finding. Test cleanup review hardened both setup operations and fresh-connection restoration.",
 "requirementMappings": ["FR-HAS-30–32/40–42", "AC-HAS-8/13 bounded subset", "IP-HAS-007 bounded subset", "TP-HAS-008/009/013/014 bounded subset", "TP-HAS-003/017 historical/migration regression subset"],
 "limitations": ["Production authorization/customer isolation/provider/budgets/retention/model quality/gates/full milestones NOT VERIFIED", "Windows/manual screenreader acceptance NOT VERIFIED", "This worker did not execute full-solution/regression/hosted CI checks; coordinator owns those"],
 "bindings": unique(integration_paths)}
(integration / "execution.json").write_text(json.dumps(integration_meta, indent=2) + "\n")

attempt = json.loads((browser / "execution/attempt-history.json").read_text())
executed = browser / "execution/verify.executed.txt"
assert binding(executed)["sha256"] == attempt["observedFinalPassingBrowserSourceSha256"]
report = json.loads((browser / "execution.json").read_text())
assert report["status"] == "PASS" and report["checks"] == 628
browser_meta = {"status": "PASS", "sourceHeadBeforeEvidenceSeal": head,
 "workerExecution": {"checks": 628, "actualExecutedBrowserSource": binding(executed), "finalFormattedBrowserSource": binding(browser / "verify.mjs"), "finalFormattedRawSourceBrowserExecutedByWorker": False,
 "difference": "One whitespace-only history locator reflow; exact diff and complete position-stripped Babel AST equality recorded. Coordinator will re-execute final raw source.", "actualHostAndAssetsExecuted": True, "hostStoppedAndPortFreeVerified": True},
 "counts": {"corruptAnalysisResponses": 29, "invalidSelectedLocks": 5, "historicalProfiles": 6, "blockedRequests": 0, "pageErrors": 0, "dialogs": 0},
 "review": "Independent integration/backend/component/coordinator inspection found no remaining runtime issue; browser exit and DB trigger cleanup review findings closed.",
 "limits": report["limitations"] + ["Original failed raw artifacts were overwritten; observed attempt codes/counts are recorded without fabricated original hashes", "Coherent intercepted NUL/CR/CRLF DOM case verifies presentation only, not trusted backend fixture eligibility"],
 "bindings": unique(browser_paths)}
(browser / "artifacts.json").write_text(json.dumps(browser_meta, indent=2) + "\n")
print(f"PASS independent metadata bindings: integration={len(integration_meta['bindings'])}; browser={len(browser_meta['bindings'])}")

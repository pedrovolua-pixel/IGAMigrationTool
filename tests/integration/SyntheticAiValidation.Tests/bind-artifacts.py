"""Bind final V8 executed evidence without recording any fixture/provider payload."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

owned = Path(__file__).resolve().parent
root = owned.parents[2]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bind(paths):
    return [{"path": str(path.relative_to(root)), "sha256": digest(path)} for path in sorted(paths)]


execution = (owned / "execution.log").read_text()
match = re.search(r"^(\d+) independent composed synthetic AI assertions passed\.", execution, re.M)
if not match or "FAIL" in execution:
    raise SystemExit("Only final successful payload-free execution may be bound.")
definitions = [path for path in owned.iterdir() if path.suffix in [".cs", ".csproj", ".json", ".md", ".py"]
               and path.name != "artifacts.json"]
module = root / "src/server/modules/SyntheticAiValidation"
source = [path for path in module.iterdir() if path.suffix in [".cs", ".csproj", ".json"]]
source += [root / path for path in ["Directory.Build.props", "global.json",
                                  "specs/003-health-assessment/synthetic-ai-fixture-contract.md"]]
binaries = [path for path in (owned / "bin/Release/net10.0").iterdir()
            if path.suffix == ".dll" or path.name.endswith((".deps.json", ".runtimeconfig.json"))]
commits = json.loads((owned / "reviewed-source.json").read_text())
metadata = {
    "version": "synthetic-ai-independent-evidence-v1",
    "status": "PASS",
    "assertions": int(match.group(1)),
    "groups": [line[5:] for line in execution.splitlines() if line.startswith("PASS ")],
    "scope": "Offline fictional fixed fake-provider composition; immutable Proposed data only",
    "reviewedCommits": commits,
    "definitionBinding": bind(definitions),
    "reviewedSourceBinding": bind(source),
    "executedBinaryBinding": bind(binaries),
    "transcriptBinding": bind([owned / "execution.log", owned / "verification.log"]),
    "toolchain": {"dotnetSdk": subprocess.check_output(
        ["/private/tmp/iga-dotnet-10.0.401/dotnet", "--version"], text=True).strip()},
    "limitations": ["No real provider/network/tools/raw resolver/storage/UI/historical run/scoring integration",
                    "Semantic redaction/authorization, factual correctness and real-model injection resistance NOT VERIFIED",
                    "Budget/recovery/retry/deletion, actual rendering, full TP-HAS-008/012, Milestone6, SEC-PILOT-004 and G1–G9 NOT VERIFIED"],
}
(owned / "artifacts.json").write_text(json.dumps(metadata, indent=2) + "\n")
print(f"Bound {metadata['assertions']} executed assertions, {len(source)} exact source files and {len(binaries)} executed binaries; no supplied payload recorded.")

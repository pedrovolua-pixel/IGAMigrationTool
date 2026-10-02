"""Independent literal fixture oracle. No imports or reads of module source/helpers."""

import hashlib
import json
from pathlib import Path

snapshot = {
    "schemaVersion": "synthetic-ai-proposal-snapshot-v1",
    "status": "Proposed",
    "runId": "11111111-2222-3333-4444-555555555555",
    "packetDigest": "a" * 64,
    "proposals": [{
        "proposalId": "proposal-01",
        "facts": [{
            "text": "Observed fixture configuration.",
            "evidenceIds": ["ev-" + "b" * 64],
            "ruleIds": ["fixture-rule-schedule-v1"],
        }],
        "inferences": [],
        "assumptions": [],
        "missingContext": [],
        "suggestions": [],
        "uncertainty": "",
        "conflictingEvidenceIds": [],
    }],
}
canonical = json.dumps(snapshot, sort_keys=True, separators=(",", ":"), ensure_ascii=True)
digest = hashlib.sha256(canonical.encode("utf-8")).hexdigest()
root = Path(__file__).resolve().parent
assert (root / "golden-snapshot.json").read_bytes() == canonical.encode("utf-8")
assert (root / "golden-digest.txt").read_text().strip() == digest
print("PASS: independent literal proposal golden bytes and SHA256.")

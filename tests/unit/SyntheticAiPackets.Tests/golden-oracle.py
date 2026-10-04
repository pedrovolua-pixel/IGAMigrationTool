"""Independent pre-execution oracle. ASCII fixture avoids runtime-specific escapes.

Run from this directory. Does not import or execute the module under test.
"""
import hashlib
import json
from pathlib import Path

fixture = json.loads(Path("golden-input.json").read_text())
assert fixture["schemaVersion"] == "synthetic-ai-fixture-input-v1"
packet = {
    "schemaVersion": "synthetic-ai-fixture-packet-v1",
    "status": "SyntheticDataOnly",
    "source": fixture["source"],
    "evidence": sorted(fixture["evidence"], key=lambda record: record["evidenceId"]),
    "ruleIds": sorted(fixture["ruleIds"]),
}
canonical = json.dumps(packet, sort_keys=True, separators=(",", ":"), ensure_ascii=True)
Path("golden-packet.json").write_bytes(canonical.encode("utf-8"))
print("GoldenBytes", len(canonical.encode("utf-8")))
print("GoldenSha256", hashlib.sha256(canonical.encode("utf-8")).hexdigest())

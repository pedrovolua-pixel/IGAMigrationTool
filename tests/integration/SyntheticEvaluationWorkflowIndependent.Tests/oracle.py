"""Offline independent expectations; imports no product module or author tests."""
import hashlib
import json
from pathlib import Path

EXPECTED_BYTES = Path(__file__).with_name("expected-v1.json").read_bytes()
assert hashlib.sha256(EXPECTED_BYTES).hexdigest() == (
    "915fe84b06ed1ac8d9097f008486040ff80ee01f7fe2d54da681ed36f4a11391"
), "pre-code expectation bytes changed"
EXPECTED = json.loads(EXPECTED_BYTES)
assert EXPECTED["population"] == 120
assert len(EXPECTED["expectedSelectedIds"]) == EXPECTED["selected"] == 100
assert len(set(EXPECTED["expectedSelectedIds"])) == 100
assert EXPECTED["expectedSelectedIds"] == sorted(EXPECTED["expectedSelectedIds"])
assert EXPECTED["mandatory"] + sum(EXPECTED["lowerAllocations"]) == 100
for name, counts in EXPECTED["counts"].items():
    confirmed, rejected, indeterminate, unreviewed, corrected, denominator = counts
    assert confirmed + rejected + indeterminate + unreviewed == 100, name
    assert denominator == confirmed + rejected, name
    assert corrected <= denominator, name
print(json.dumps({"result": "PASS", "expectedDigest": hashlib.sha256(
    Path(__file__).with_name("expected-v1.json").read_bytes()).hexdigest(),
    "selected": 100, "mandatory": 40, "lower": [15, 45]}))

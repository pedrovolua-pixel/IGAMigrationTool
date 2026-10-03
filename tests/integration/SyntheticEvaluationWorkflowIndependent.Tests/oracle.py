"""Offline independent expectations; imports no product module or author tests."""
import hashlib
import json
from pathlib import Path

EXPECTED = json.loads(Path(__file__).with_name("expected-v1.json").read_text())
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

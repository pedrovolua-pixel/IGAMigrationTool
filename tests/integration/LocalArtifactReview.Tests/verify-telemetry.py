"""Inspect original fictional host telemetry and regenerate owned absence proof; never generate expected fixtures."""
import hashlib,json
from pathlib import Path
r=Path(__file__).resolve().parents[3]
w=r/'tests/e2e/artifact-review'
log=(w/'.host/host-runtime.log').read_text()
actual=json.loads((w/'.host/observed/final-saved-source-and-replay.json').read_text())
reasons=[event['reason'] for entry in actual['finalAnalysis']['artifactReview']['artifacts'] for event in entry['history']]
texts=[artifact['text'] for package in actual['finalAnalysis']['fixPackages']['snapshot']['packages'] for option in package['options'] for artifact in option['artifacts']]
if any(value in log for value in reasons+texts):raise SystemExit('FAIL fixture-payload-present-in-host-telemetry')
proof=dict(schema='v13-original-host-telemetry-absence-v1',status='PASS',reasonValuesChecked=len(reasons),artifactValuesChecked=len(texts),hostLogBytes=(w/'.host/host-runtime.log').stat().st_size,hostLogSha256=hashlib.sha256((w/'.host/host-runtime.log').read_bytes()).hexdigest(),scope='Exact recorded fictional reason and original artifact text absence; no production/audit-provider claim')
(r/'tests/integration/LocalArtifactReview.Tests/telemetry.json').write_text(json.dumps(proof,indent=2)+'\n')
print('PASS original host telemetry payload absence; reasons='+str(len(reasons))+'; artifacts='+str(len(texts)))

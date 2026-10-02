# Private developer OCI image inventories (BFF-DP02)

This packet implements the affected image-inventory portion of IP-HAS-001 and the approved [change/merge checks](../../../specs/003-health-assessment/implementation-plan.md#mandatory-ci-and-evidence-gates). It collects developer evidence; it cannot authorize promotion, merge, Azure publication, workload permissions, sign-in or deployment. It introduces no application endpoint, dependency, vulnerability threshold, license allowlist, scan-database age limit or production signing authority.

## Sources and tool selection

The [tool lock](../../../infra/containers/image-evidence-tools.lock.json) fixes the direct Linux AMD64 Trivy **0.73.0** binary, its official release archive, checksum file and publisher-bundle hashes. The official [immutable release](https://github.com/aquasecurity/trivy/releases/tag/v0.73.0) was published on 2026-08-03 from `40c73e5d6166dcc0346a1ab4e94499d1572854e4`; the GitHub commit signature is reported verified. On 2026-10-02 the official release API SHA-256 values were checked against downloaded bytes, the checksum-list entry and the extracted regular binary. These verified input identities are recorded in the lock.

The official [March compromise advisory](https://github.com/aquasecurity/trivy/security/advisories/GHSA-69fq-xp46-6x23) covers the historical malicious release/actions; this packet uses no Trivy/setup-trivy GitHub Action, historical 0.69.4 binary or mutable scanner image/tag. The [path traversal advisory](https://github.com/aquasecurity/trivy/security/advisories/GHSA-mcj4-mphf-j9ff) identifies versions before 0.71.1, so the selected version is outside that range. The collector explicitly selects Aqua's `ghcr.io/aquasecurity/trivy-db:2` source and removes inherited scanner configuration/environment overrides. Review current official advisories when changing the pin and before release use. This limited source review is not a blanket assertion that the scanner has no vulnerabilities.

The publisher Sigstore bundle's hash and message digest match the archive. **Its cryptographic trust chain was not independently verified** and remains `NOT_VERIFIED`. Download/commit metadata and a checksum do not supply the production signing authority required by [ADR-0005](../../../architecture/decisions/ADR-0005-gate-evidence-signing.md). Publisher verification must be reviewed before release reliance.

Official implementation references: [Trivy SBOM](https://trivy.dev/docs/latest/guide/supply-chain/sbom/), [license scanner](https://trivy.dev/docs/latest/scanner/license/), [pinned report shape](https://github.com/aquasecurity/trivy/blob/v0.73.0/pkg/types/report.go), [pinned SPDX producer](https://github.com/aquasecurity/trivy/blob/v0.73.0/pkg/sbom/spdx/marshal.go), [pinned image-to-root encoder](https://github.com/aquasecurity/trivy/blob/v0.73.0/pkg/sbom/io/encode.go). Tool and database metadata are checked against the official shapes. Trivy's built-in license classifications/confidence are detection metadata, not approved repository license policy.

## What collection proves

The collector verifies the exact scanner binary hash and reported version, exact clean checkout commit and tracked build/lock inputs, and a local Linux AMD64 Docker **config ID**. It scans that config ID using Docker only; it never pulls the application image from a registry. It requests all packages, vulnerabilities and full license scanning, without a severity filter, ignored-unfixed policy or license exclusion. Detected unknown/missing package-license metadata is counted; the result cannot prove that every possible component/license was detected.

SPDX 2.3 JSON is converted from the validated inventory so both outputs describe the same scan. The collector validates report image/tool/schema/times, nonempty package inventory, SPDX producer/schema/packages/times, exactly one DOCUMENT DESCRIBES CONTAINER root with the scanned config ID annotation, duplicate JSON/SPDX identifiers, actual vulnerability-database bytes and metadata, and unchanged source/tool/image/report/database across collection. Database updated/downloaded timestamps cannot be in the future; the publisher's next-update timestamp may be in the future. Database age/freshness acceptance remains `NOT_VERIFIED`; no new maximum age is invented.

A local config ID, observed Docker repository digest and registry-published manifest digest are different identities. Observed Docker repository digests are recorded without repository names; `publishedManifestDigest` stays null. Source and lock hashes describe the checkout at collection, **not verified source-to-image build provenance**. The SPDX root is structurally bound to the scanned config ID through the pinned producer’s `ImageID: sha256:...` annotation and DOCUMENT DESCRIBES relationship. This does not prove package completeness, that every package belongs to the scanned image, or source-to-image build provenance. The provenance record is unsigned collection metadata, not a SLSA or gate signature.

`collection: PASS` means successful inventory collection and structural/identity checks. Vulnerability acceptance, license acceptance, signed provenance, source-to-image build binding, restricted-store evidence, reviewers, compatibility, registry publication, promotion and Azure deployment stay `NOT_VERIFIED`, even if an inventory contains no findings.

## Linux CI preparation and invocation

The developer Mac has no Docker engine; actual image collection must run on the container-capable Linux CI runner. No Azure or registry credential is needed. Coordinator owns workflow integration and must use the actual reviewed BFF Dockerfile and every dependency lock used in that image.

From a clean checkout of the exact tested commit, install only the locked binary in the runner's temporary directory. The following uses the committed lock rather than a mutable installation script/action; it checks archive bytes before reading one regular member, and checks binary bytes before marking executable:

```sh
python3 - "$RUNNER_TEMP/trivy" <<'PY'
import hashlib
import json
from pathlib import Path
import sys
import tarfile
import tempfile
import urllib.request

lock = json.loads(Path('infra/containers/image-evidence-tools.lock.json').read_text())['trivy']
with urllib.request.urlopen(lock['archiveUrl'], timeout=120) as response:
    archive = response.read()
if hashlib.sha256(archive).hexdigest() != lock['archiveSha256']:
    raise SystemExit('Scanner archive digest mismatch')
with tempfile.TemporaryFile() as stream:
    stream.write(archive)
    stream.seek(0)
    with tarfile.open(fileobj=stream, mode='r:gz') as package:
        member = package.getmember('trivy')
        if not member.isfile():
            raise SystemExit('Scanner member is not a regular file')
        binary = package.extractfile(member).read()
if hashlib.sha256(binary).hexdigest() != lock['binarySha256']:
    raise SystemExit('Scanner binary digest mismatch')
target = Path(sys.argv[1])
with target.open('xb') as stream:
    stream.write(binary)
target.chmod(0o700)
PY
```

After the reviewed disabled BFF image build and runtime checks, substitute the **actual** local image tag, reviewed Dockerfile and host/module lock paths in this invocation. The example deliberately leaves those environment-specific names unresolved:

```sh
python3 infra/containers/collect-image-evidence.py \
  --image "$(docker image inspect '<actual-disabled-BFF-local-tag>' --format '{{.Id}}')" \
  --trivy "$RUNNER_TEMP/trivy" \
  --source-sha "$(git rev-parse HEAD)" \
  --input-lock global.json \
  --input-lock Directory.Build.props \
  --input-lock infra/containers/azure-development-bootstrap.images.lock.json \
  --input-lock infra/containers/image-evidence-tools.lock.json \
  --input-lock 'infra/containers/<actual-BFF>.Dockerfile' \
  --input-lock 'src/server/hosts/<actual-BFF-host>/packages.lock.json' \
  --input-lock src/server/hosts/BffFoundation/packages.lock.json \
  --input-lock src/server/modules/IdentitySessions/packages.lock.json \
  --input-lock src/server/modules/IdentityPolicy/packages.lock.json \
  --output "$RUNNER_TEMP/bff-image-evidence"
```

The required core lock inputs reuse the existing approved Microsoft image baseline. Include other files/locks used in the actual reviewed build with repeated `--input-lock`; the collector does not infer a Dockerfile dependency graph. It refuses a dirty source checkout, omitted core locks, no Dockerfile/package lock, duplicate or unsafe input paths, symlinks, wrong commit and a non-config image identifier.

## Output boundary and failure handling

The output directory must be new and **outside the repository**. Directory mode is 0700; summary/report/log files are 0600 and subprocess files use a restrictive umask. Raw `inventory.json`, `sbom.spdx.json`, scanner cache and `private-process.log` are private temporary outputs. They can contain detailed findings, package paths or image configuration. **Never upload them to public GitHub artifacts, print them in CI, commit them or include them in the pilot board.** Sanitized summary metadata also remains local until the coordinator reviews a separately bounded metadata record. Production bundles require the sanitizer and restricted evidence-store integration under the approved plan.

Stdout is one fixed collection-status sentence. Failures use a fixed category, never scanner exception text, request headers, environment, findings or report strings. A failed collection has no completed summary; treat partial temporary outputs as unusable. Tool/process/database/output failures fail collection. No `continue-on-error`, `|| true`, fallback scanner, scan suppression, signing fixture, artifact upload, registry push or grant is part of this packet.

Run focused checks:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/infrastructure/ImageEvidence -p 'test_*.py' -v
python3 infra/containers/collect-image-evidence.py --help
```

The synthetic tests cover actual collector composition through a controlled process double plus malformed/mismatched inputs, unknown license/finding retention, unrelated/missing/wrong/duplicate SPDX roots and image annotations, future report/database/SBOM clocks, database absence/empty bytes, wrong binaries and symlinks, unsafe paths/output, private permissions, protected diagnostics, and explicit non-promotion status. They do not claim an executed Docker/Trivy scan, scanner detection accuracy, release signature, private registry pull or any G1–G9 acceptance.

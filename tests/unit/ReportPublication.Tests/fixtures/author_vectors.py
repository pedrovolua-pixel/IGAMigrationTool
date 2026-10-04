#!/usr/bin/env python3
"""Independent original vector author. Never consumes a production codec.

This initial authoring program is retained for provenance. Committed fixture
bytes and literal goldens, not a regenerated run, are the integration oracle.
"""
import copy
import hashlib
import json
from pathlib import Path


def uid(number):
    return f"00000000-0000-4000-8000-{number:012d}"


def sha(value):
    return hashlib.sha256(value).hexdigest()


def encoded(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True,
                      allow_nan=False).encode("utf-8")


def metric(value):
    return {"availability": "Available", "value": value, "reason": "None"}


def unavailable_estimate():
    return {"availability": "Unavailable", "value": None,
            "reason": "NotAssessed", "sourceReference": None}


fields = sorted(["inputs", "executiveSummary", "environmentScope", "scores", "maturity",
                 "dimensions", "coverage", "findings", "rootCauses", "healthyControls",
                 "recommendations", "acceptedRisks", "warnings", "methodology",
                 "technicalAppendices.notes", "technicalAppendices.protectedReferences",
                 "redactionMarkers"])
scope = {"customerId": uid(1), "projectId": uid(2), "environmentId": uid(3),
         "assessmentId": uid(4)}
actor = {"tenantId": uid(5), "objectId": uid(6), "sessionId": uid(7),
         "securityVersion": "3"}
inputs = {"baselineId": uid(10), "baselineDigest": "11" * 32,
          "capabilityLockDigest": "12" * 32, "ruleCatalogVersion": "catalog-v1",
          "ruleCatalogDigest": "13" * 32, "scoringProfileVersion": "profile-v1",
          "scoringProfileDigest": "14" * 32, "maturityProfileVersion": "maturity-profile-v1",
          "maturityProfileDigest": "15" * 32, "desiredOutcomeVersion": None,
          "desiredOutcomeDigest": None, "scoringAlgorithmVersion": "pilot-health-v1",
          "maturityAlgorithmVersion": "pilot-maturity-v1", "applicationVersion": "app-v1",
          "reviewSnapshotDigest": "16" * 32, "coverageSnapshotDigest": "17" * 32,
          "runInputDigest": "18" * 32, "aiPolicyVersion": "ai-disabled-v1",
          "modelVersion": None, "promptVersion": None}
retention = {"policyId": uid(11), "policyVersion": "policy-v1", "class": "PublishedArtifact",
             "clockStartUtc": "2026-10-03T12:00:00.0000000Z",
             "expiresAtUtc": "2026-10-04T12:00:00.0000000Z", "holdReference": None}
provenance = [{"kind": "Baseline", "opaqueRecordId": uid(10), "digest": "11" * 32},
              {"kind": "Review", "opaqueRecordId": uid(12), "digest": "16" * 32}]
score = {"schemaVersion": "health-report-score-v1", "provisional": metric("72.5"),
         "publishable": metric("72.5"), "quality": metric("100")}
projection = {"schemaVersion": "health-report-projection-v1",
              "scope": {k: v for k, v in scope.items() if k != "assessmentId"},
              "assessmentId": scope["assessmentId"], "runId": uid(20), "runRevision": "9",
              "inputs": inputs,
              "executiveSummary": [{"id": uid(21), "category": "controls", "title": "Résumé — review",
                                     "text": 'Measured control: café 👩🏽‍💻; line\nnext\t"quote"\\path\u0000\u001f',
                                     "availability": "Available", "reason": "None"}],
              "environmentScope": [{"id": uid(22), "category": "controls", "title": "Scope",
                                    "text": "Approved assessment boundary", "availability": "Available",
                                    "reason": "None"}],
              "scores": score,
              "maturity": {"availability": "Available", "level": "Defined",
                           "algorithmVersion": "pilot-maturity-v1", "inputDigest": "19" * 32,
                           "contentDigest": "20" * 32, "reason": "None"},
              "dimensions": [{"kind": "Category", "id": uid(23), "category": "controls",
                              "provisional": metric("72.5"), "publishable": metric("72.5")}],
              "coverage": {"items": [{"id": uid(24), "category": "controls", "state": "Finding", "reason": "None"}],
                           "counts": {"planned": "1", "executed": "1", "gap": "0", "notApplicable": "0"}},
              "findings": [{"id": uid(30), "category": "controls", "title": "Control review",
                            "summary": "Periodic confirmation is incomplete", "severity": "Medium",
                            "state": "AutoConfirmed", "method": "Deterministic",
                            "confidencePercent": "100", "confidenceBand": "Certain",
                            "confidenceAvailability": "Available", "confidenceReason": "None",
                            "mandatoryReview": False, "referenceIds": [uid(31)],
                            "rootCauseIds": [uid(32)], "originalDigest": "21" * 32, "reviewRevision": "0"}],
              "rootCauses": [{"id": uid(32), "category": "controls", "summary": "Periodic review lacks confirmation",
                              "findingIds": [uid(30)], "availability": "Available", "reason": "None"}],
              "healthyControls": [{"id": uid(33), "category": "controls", "ruleId": "CONTROL-002",
                                   "ruleVersion": "1.0", "summary": "Measurement is present"}],
              "recommendations": [{"id": uid(34), "findingId": uid(30), "category": "controls",
                                   "summary": "Review control measurement", "priority": unavailable_estimate(),
                                   "effort": unavailable_estimate(), "reviewState": "Unverified"}],
              "acceptedRisks": [], "warnings": [],
              "methodology": [{"id": uid(35), "category": "controls", "title": "Methodology",
                               "text": "Frozen rule and score inputs", "availability": "Available", "reason": "None"}],
              "technicalAppendices": {"notes": [{"id": uid(36), "category": "controls", "title": "Versions",
                                                 "text": "Catalog and profile versions are frozen",
                                                 "availability": "Available", "reason": "None"}],
                                      "protectedReferences": [{"id": uid(31), "category": "controls",
                                                               "availability": "Available", "reason": "None"}]},
              "redactionMarkers": []}
source = {"scope": scope, "runId": uid(20), "runRevision": "9", "assessmentState": "Completed",
          "inputs": inputs, "projection": projection, "score": score, "warnings": [],
          "retention": retention, "requiredCategories": ["controls"], "requiredFields": fields,
          "provenance": provenance}


def produce(name, source_value, number):
    source_value = copy.deepcopy(source_value)
    projection_value, score_value = source_value["projection"], source_value["score"]
    source_hash, projection_hash, score_hash = map(sha, map(encoded, [source_value, projection_value, score_value]))
    command = {"actor": actor, "operationId": uid(100 + number), "scope": scope,
               "runId": uid(20), "expectedRunRevision": source_value["runRevision"],
               "expectedSourceDigest": source_hash, "acknowledgedWarnings": source_value["warnings"]}
    manifest = {"schemaVersion": "health-report-manifest-v1", "projectionSchemaVersion": "health-report-projection-v1",
                "scoreSchemaVersion": "health-report-score-v1",
                "scope": projection_value["scope"], "assessmentId": scope["assessmentId"],
                "reportVersionId": uid(200 + number), "runId": uid(20), "runRevision": source_value["runRevision"],
                "createdAtUtc": "2026-10-03T12:00:01.0000000Z",
                "createdBy": {k: actor[k] for k in ["tenantId", "objectId"]},
                "assessmentState": source_value["assessmentState"],
                "approvalState": "PublishedWithWarnings" if source_value["warnings"] else "Published",
                "inputs": source_value["inputs"], "projectionDigest": projection_hash,
                "scoreDigest": score_hash, "sourceDigest": source_hash,
                "requiredCategories": source_value["requiredCategories"], "requiredFields": fields,
                "classification": "MinimizedDerivedReport", "redactionMarkers": projection_value["redactionMarkers"],
                "retention": source_value["retention"], "provenance": source_value["provenance"],
                "artifactInputs": [{"kind": "Projection", "digest": projection_hash, "byteLength": str(len(encoded(projection_value)))},
                                   {"kind": "Score", "digest": score_hash, "byteLength": str(len(encoded(score_value)))}]}
    manifest_hash = sha(encoded(manifest))
    event = {"schemaVersion": "report-publication-audit-v1", "eventId": uid(300 + number),
             "streamId": uid(400 + number), "writerBindingReference": uid(500 + number),
             "sequence": "1", "previousEventDigest": "0" * 64,
             "eventAtUtc": "2026-10-03T12:00:01.0000000Z", "operationId": command["operationId"],
             "invocationId": uid(600 + number), "correlationId": uid(700 + number),
             "actorKind": "Human", "actor": actor, "scope": scope, "resourceKind": "Run", "resourceId": uid(20),
             "action": "Publish", "outcome": "Succeeded", "reason": "None", "manifestDigest": manifest_hash,
             "returnedFields": [], "redactedFields": []}
    receipt = {"schemaVersion": "report-publication-receipt-v1", "operationId": command["operationId"],
               "actor": actor, "scope": scope, "runId": uid(20), "expectedRunRevision": source_value["runRevision"],
               "commandDigest": sha(encoded(command)), "reportVersionId": manifest["reportVersionId"],
               "manifestDigest": manifest_hash, "projectionDigest": projection_hash, "scoreDigest": score_hash,
               "committedAtUtc": event["eventAtUtc"], "eventId": event["eventId"], "eventDigest": sha(encoded(event))}
    read_request = {"schemaVersion": "report-exact-read-request-v1", "actor": actor, "scope": scope,
                    "reportVersionId": manifest["reportVersionId"], "expectedManifestDigest": manifest_hash,
                    "invocationId": uid(800 + number)}
    read_event = copy.deepcopy(event)
    read_event.update(eventId=uid(900 + number), sequence="2", previousEventDigest=sha(encoded(event)),
                      operationId=None, invocationId=read_request["invocationId"], correlationId=uid(1000 + number),
                      resourceKind="ReportVersion", resourceId=manifest["reportVersionId"], action="ReadExact",
                      returnedFields=fields, eventAtUtc="2026-10-03T12:00:02.0000000Z")
    read_receipt = {"schemaVersion": "report-exact-read-receipt-v1", "invocationId": read_request["invocationId"],
                    "requestDigest": sha(encoded(read_request)), "actor": actor, "scope": scope,
                    "reportVersionId": manifest["reportVersionId"], "manifestDigest": manifest_hash,
                    "committedAtUtc": read_event["eventAtUtc"], "eventId": read_event["eventId"],
                    "eventDigest": sha(encoded(read_event))}
    return {f"{name}.{kind}.json": value for kind, value in {
        "source": source_value, "projection": projection_value, "score": score_value,
        "command": command, "manifest": manifest, "publish-event": event, "receipt": receipt,
        "read-request": read_request, "read-event": read_event, "read-receipt": read_receipt}.items()}


warned = copy.deepcopy(source)
warned["assessmentState"] = "CompletedWithGaps"
warned["inputs"].update(aiPolicyVersion="ai-constrained-v1", modelVersion="model-v1", promptVersion="prompt-v1")
warned["score"].update(provisional=metric("45.125"), publishable=metric("82"), quality=metric("50"))
warned["projection"]["findings"][0].update(severity="High", method="AI", state="Proposed",
                                         confidencePercent="80", confidenceBand="High", mandatoryReview=True)
warned["projection"]["coverage"]["items"].append({"id": uid(25), "category": "controls", "state": "Error", "reason": "Error"})
warned["projection"]["coverage"]["counts"].update(planned="2", gap="1")
warned["warnings"] = [{"kind": "CoverageIncomplete", "recordId": uid(25), "category": "controls"},
                      {"kind": "MandatoryReviewIncomplete", "recordId": uid(30), "category": "controls"}]
warned["projection"]["warnings"] = warned["warnings"]
warned["projection"]["rootCauses"][0].update(summary=None, availability="Unavailable", reason="InsufficientEvidence")
warned["projection"]["technicalAppendices"]["protectedReferences"][0].update(availability="Unavailable", reason="Expired")
warned["projection"]["redactionMarkers"] = [{"section": "technicalAppendices.protectedReferences", "id": uid(31),
                                             "field": "availability", "reason": "Expired"}]

limited = copy.deepcopy(source)
limited["assessmentState"] = "CompletedWithGaps"
limited["warnings"] = [{"kind": "SourceLimitation", "recordId": uid(12), "category": "controls"}]
limited["projection"]["warnings"] = limited["warnings"]
vectors = produce("completed", source, 1) | produce("warned", warned, 2) | produce("source-limitation", limited, 3)
deny = {"schemaVersion": "report-publication-audit-v1", "eventId": uid(1101), "streamId": uid(1102),
        "writerBindingReference": uid(1103), "sequence": "1", "previousEventDigest": "0" * 64,
        "eventAtUtc": "2026-10-03T12:00:03.0000000Z", "operationId": None,
        "invocationId": uid(1104), "correlationId": uid(1105), "actorKind": "Anonymous", "actor": None,
        "scope": None, "resourceKind": None, "resourceId": None, "action": "ReadExact", "outcome": "Denied",
        "reason": "AuthorityDenied", "manifestDigest": None, "returnedFields": [], "redactedFields": []}
vectors["anonymous-denial.audit.json"] = deny

if __name__ == "__main__":
    destination = Path(__file__).parent
    records = {}
    for filename, value in sorted(vectors.items()):
        data = encoded(value)
        (destination / filename).write_bytes(data)
        records[filename] = {"sha256": sha(data), "byteLength": len(data)}
    manifest = {"schemaVersion": "independent-native-publication-goldens-v1", "files": records,
                "authority": "Fictional local source; no runtime or pilot acceptance"}
    (destination / "goldens.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Authored {len(records)} independent original canonical vectors.")

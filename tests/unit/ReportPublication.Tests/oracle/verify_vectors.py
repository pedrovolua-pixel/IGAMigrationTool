#!/usr/bin/env python3
"""Independent strict byte/shape/link oracle; no production-code imports."""
import copy
import hashlib
import json
import re
from datetime import datetime
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "fixtures"
GUID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
DIGEST = re.compile(r"^[0-9a-f]{64}$")
TOKEN = re.compile(r"^[A-Za-z0-9._-]{1,128}$")
UTC = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$")
COUNTER = re.compile(r"^(0|[1-9][0-9]*)$")
DECIMAL = re.compile(r"^(0|[1-9][0-9]*)(\.[0-9]*[1-9])?$")
REASONS = set("None NotApplicable NotAssessed InsufficientEvidence Excluded Inaccessible Redacted Unsupported Error Expired Deleted".split())
STATES = set("Proposed AutoConfirmed Confirmed Rejected Deferred AcceptedRisk RemediationPlanned InProgress RemediatedPendingValidation ValidatedClosed Reopened".split())
COVERAGE = set("Pass Finding NotApplicable NotAssessed InsufficientEvidence Excluded Inaccessible Redacted Unsupported Error".split())
FIELDS = sorted("inputs executiveSummary environmentScope scores maturity dimensions coverage findings rootCauses healthyControls recommendations acceptedRisks warnings methodology technicalAppendices.notes technicalAppendices.protectedReferences redactionMarkers".split())
INPUT_FIELDS = "baselineId baselineDigest capabilityLockDigest ruleCatalogVersion ruleCatalogDigest scoringProfileVersion scoringProfileDigest maturityProfileVersion maturityProfileDigest desiredOutcomeVersion desiredOutcomeDigest scoringAlgorithmVersion maturityAlgorithmVersion applicationVersion reviewSnapshotDigest coverageSnapshotDigest runInputDigest aiPolicyVersion modelVersion promptVersion".split()
SOURCE_FIELDS = "scope runId runRevision assessmentState inputs projection score warnings retention requiredCategories requiredFields provenance".split()
PROJECTION_FIELDS = "schemaVersion scope assessmentId runId runRevision inputs executiveSummary environmentScope scores maturity dimensions coverage findings rootCauses healthyControls recommendations acceptedRisks warnings methodology technicalAppendices redactionMarkers".split()
MANIFEST_FIELDS = "schemaVersion projectionSchemaVersion scoreSchemaVersion scope assessmentId reportVersionId runId runRevision createdAtUtc createdBy assessmentState approvalState inputs projectionDigest scoreDigest sourceDigest requiredCategories requiredFields classification redactionMarkers retention provenance artifactInputs".split()
EVENT_FIELDS = "schemaVersion eventId streamId writerBindingReference sequence previousEventDigest eventAtUtc operationId invocationId correlationId actorKind actor scope resourceKind resourceId action outcome reason manifestDigest returnedFields redactedFields".split()
RECEIPT_FIELDS = "schemaVersion operationId actor scope runId expectedRunRevision commandDigest reportVersionId manifestDigest projectionDigest scoreDigest committedAtUtc eventId eventDigest".split()
COMMAND_FIELDS = "actor operationId scope runId expectedRunRevision expectedSourceDigest acknowledgedWarnings".split()
READ_FIELDS = "schemaVersion actor scope reportVersionId expectedManifestDigest invocationId".split()
READ_RECEIPT_FIELDS = "schemaVersion invocationId requestDigest actor scope reportVersionId manifestDigest committedAtUtc eventId eventDigest".split()


def require(condition, label):
    if not condition:
        raise ValueError(label)


def closed(value, names):
    require(type(value) is dict and set(value) == set(names), "closed-fields")


def text(value):
    require(type(value) is str and len(value) <= 4096, "text")
    require(all(not 0xD800 <= ord(c) <= 0xDFFF for c in value), "unicode-scalar")


def token(value):
    require(type(value) is str and TOKEN.fullmatch(value), "token")


def uuid(value):
    require(type(value) is str and GUID.fullmatch(value) and value != "00000000-0000-0000-0000-000000000000", "uuid")


def digest(value):
    require(type(value) is str and DIGEST.fullmatch(value), "digest")


def counter(value, positive=False):
    require(type(value) is str and COUNTER.fullmatch(value), "counter")
    require(int(value) <= 9223372036854775807 and (not positive or int(value) > 0), "counter-bound")


def decimal(value):
    require(type(value) is str and DECIMAL.fullmatch(value), "decimal")
    require(("." not in value or len(value.split(".")[1]) <= 28) and Decimal(value) <= 100, "decimal-bound")


def utc(value):
    require(type(value) is str and UTC.fullmatch(value), "utc")
    datetime.strptime(value[:26] + "Z", "%Y-%m-%dT%H:%M:%S.%fZ")


def enum(value, permitted):
    require(type(value) is str and value in permitted, "enum")


def array(value):
    require(type(value) is list, "array")


def sorted_set(values, identity=lambda x: x):
    array(values)
    try:
        keys = [identity(x) for x in values]
    except (KeyError, TypeError):
        raise ValueError("set-shape") from None
    require(keys == sorted(keys) and len(set(keys)) == len(keys), "set-order-identity")


def scope(value, assessment=True):
    closed(value, "customerId projectId environmentId assessmentId".split() if assessment else "customerId projectId environmentId".split())
    for member in value.values():
        uuid(member)


def actor(value):
    closed(value, "tenantId objectId sessionId securityVersion".split())
    for name in ["tenantId", "objectId", "sessionId"]:
        uuid(value[name])
    counter(value["securityVersion"], True)


def availability(value, field, kind="decimal"):
    enum(value["availability"], {"Available", "Unavailable"})
    enum(value["reason"], REASONS)
    if value["availability"] == "Available":
        require(value["reason"] == "None" and value[field] is not None, "available")
        (decimal if kind == "decimal" else text)(value[field])
    else:
        require(value["reason"] != "None" and value[field] is None, "unavailable")


def metric(value):
    closed(value, ["availability", "value", "reason"])
    availability(value, "value")


def score(value):
    closed(value, ["schemaVersion", "provisional", "publishable", "quality"])
    require(value["schemaVersion"] == "health-report-score-v1", "score-version")
    for name in ["provisional", "publishable", "quality"]:
        metric(value[name])


def inputs(value):
    closed(value, INPUT_FIELDS)
    uuid(value["baselineId"])
    require((value["desiredOutcomeVersion"] is None) == (value["desiredOutcomeDigest"] is None), "outcome-pair")
    for name, member in value.items():
        if name == "baselineId":
            continue
        if member is None:
            require(name in {"desiredOutcomeVersion", "desiredOutcomeDigest", "modelVersion", "promptVersion"}, "required-input")
        else:
            (digest if name.endswith("Digest") else token)(member)


def retention(value):
    closed(value, "policyId policyVersion class clockStartUtc expiresAtUtc holdReference".split())
    uuid(value["policyId"])
    token(value["policyVersion"])
    require(value["class"] == "PublishedArtifact", "retention-class")
    utc(value["clockStartUtc"])
    utc(value["expiresAtUtc"])
    require(value["clockStartUtc"] <= value["expiresAtUtc"], "retention-order")
    if value["holdReference"] is not None:
        uuid(value["holdReference"])


def warnings(value):
    sorted_set(value, lambda x: (x["kind"], x["recordId"], x["category"]))
    for row in value:
        closed(row, ["kind", "recordId", "category"])
        enum(row["kind"], {"MandatoryReviewIncomplete", "CoverageIncomplete", "SourceLimitation"})
        uuid(row["recordId"])
        token(row["category"])


def provenance(value):
    sorted_set(value, lambda x: (x["kind"], x["opaqueRecordId"]))
    for row in value:
        closed(row, ["kind", "opaqueRecordId", "digest"])
        token(row["kind"])
        uuid(row["opaqueRecordId"])
        digest(row["digest"])


def field_set(value):
    require(value == FIELDS, "required-field-registry")


def display(value):
    array(value)
    ids = []
    for row in value:
        closed(row, ["id", "category", "title", "text", "availability", "reason"])
        uuid(row["id"])
        token(row["category"])
        text(row["title"])
        availability(row, "text", "text")
        ids.append(row["id"])
    require(len(ids) == len(set(ids)), "display-identity")


def links(value):
    sorted_set(value)
    for member in value:
        uuid(member)


def estimate(value):
    closed(value, ["availability", "value", "reason", "sourceReference"])
    availability(value, "value", "text")
    if value["availability"] == "Available":
        uuid(value["sourceReference"])
    else:
        require(value["sourceReference"] is None, "estimate-unavailable-reference")


def projection(value):
    closed(value, PROJECTION_FIELDS)
    require(value["schemaVersion"] == "health-report-projection-v1", "projection-version")
    scope(value["scope"], False)
    uuid(value["assessmentId"])
    uuid(value["runId"])
    counter(value["runRevision"], True)
    inputs(value["inputs"])
    score(value["scores"])
    for name in ["executiveSummary", "environmentScope", "methodology"]:
        display(value[name])
    maturity = value["maturity"]
    closed(maturity, "availability level algorithmVersion inputDigest contentDigest reason".split())
    token(maturity["algorithmVersion"])
    digest(maturity["inputDigest"])
    digest(maturity["contentDigest"])
    availability(maturity, "level", "text")
    if maturity["level"] is not None:
        enum(maturity["level"], {"Initial", "Developing", "Defined", "Managed", "Optimized"})
    sorted_set(value["dimensions"], lambda x: (x["kind"], x["id"]))
    for row in value["dimensions"]:
        closed(row, ["kind", "id", "category", "provisional", "publishable"])
        enum(row["kind"], {"Category", "ObjectType", "Module", "DesiredOutcome"})
        uuid(row["id"])
        token(row["category"])
        metric(row["provisional"])
        metric(row["publishable"])
    coverage = value["coverage"]
    closed(coverage, ["items", "counts"])
    closed(coverage["counts"], ["planned", "executed", "gap", "notApplicable"])
    for member in coverage["counts"].values():
        counter(member)
    sorted_set(coverage["items"], lambda x: x["id"])
    counted = {"executed": 0, "gap": 0, "notApplicable": 0}
    for row in coverage["items"]:
        closed(row, ["id", "category", "state", "reason"])
        uuid(row["id"])
        token(row["category"])
        enum(row["state"], COVERAGE)
        require(row["reason"] == ("None" if row["state"] in {"Pass", "Finding"} else row["state"]), "coverage-reason")
        counted["executed" if row["state"] in {"Pass", "Finding"} else "notApplicable" if row["state"] == "NotApplicable" else "gap"] += 1
    require(int(coverage["counts"]["planned"]) == len(coverage["items"]), "coverage-planned")
    require(all(int(coverage["counts"][k]) == count for k, count in counted.items()), "coverage-counts")
    sorted_set(value["findings"], lambda x: x["id"])
    for row in value["findings"]:
        closed(row, "id category title summary severity state method confidencePercent confidenceBand confidenceAvailability confidenceReason mandatoryReview referenceIds rootCauseIds originalDigest reviewRevision".split())
        uuid(row["id"])
        token(row["category"])
        text(row["title"])
        text(row["summary"])
        enum(row["severity"], {"Critical", "High", "Medium", "Low", "Informational"})
        enum(row["state"], STATES)
        enum(row["method"], {"Deterministic", "AI"})
        require(type(row["mandatoryReview"]) is bool, "boolean")
        availability({"availability": row["confidenceAvailability"], "value": row["confidencePercent"],
                      "reason": row["confidenceReason"]}, "value")
        if row["confidenceAvailability"] == "Available":
            token(row["confidenceBand"])
        else:
            require(row["confidenceBand"] is None, "confidence-unavailable-band")
        links(row["referenceIds"])
        links(row["rootCauseIds"])
        require(row["rootCauseIds"], "root-cause-required")
        digest(row["originalDigest"])
        counter(row["reviewRevision"])
    sorted_set(value["rootCauses"], lambda x: x["id"])
    for row in value["rootCauses"]:
        closed(row, ["id", "category", "summary", "findingIds", "availability", "reason"])
        uuid(row["id"])
        token(row["category"])
        availability(row, "summary", "text")
        links(row["findingIds"])
    findings = {x["id"]: x for x in value["findings"]}
    causes = {x["id"]: x for x in value["rootCauses"]}
    for finding_id, row in findings.items():
        require(all(c in causes and finding_id in causes[c]["findingIds"] for c in row["rootCauseIds"]), "root-link")
    for cause_id, row in causes.items():
        require(all(f in findings and cause_id in findings[f]["rootCauseIds"] for f in row["findingIds"]), "finding-link")
    sorted_set(value["healthyControls"], lambda x: x["id"])
    for row in value["healthyControls"]:
        closed(row, ["id", "category", "ruleId", "ruleVersion", "summary"])
        uuid(row["id"])
        for name in ["category", "ruleId", "ruleVersion"]:
            token(row[name])
        text(row["summary"])
    sorted_set(value["recommendations"], lambda x: x["id"])
    for row in value["recommendations"]:
        closed(row, ["id", "findingId", "category", "summary", "priority", "effort", "reviewState"])
        uuid(row["id"])
        require(row["findingId"] in findings, "recommendation-link")
        token(row["category"])
        text(row["summary"])
        estimate(row["priority"])
        estimate(row["effort"])
        enum(row["reviewState"], {"Unverified", "Reviewed"})
    sorted_set(value["acceptedRisks"], lambda x: x["id"])
    for row in value["acceptedRisks"]:
        closed(row, ["id", "findingId", "category", "decisionReference", "reviewAtUtc", "status"])
        uuid(row["id"])
        uuid(row["decisionReference"])
        require(row["findingId"] in findings, "risk-link")
        token(row["category"])
        utc(row["reviewAtUtc"])
        enum(row["status"], {"Current", "ReviewRequired"})
    warnings(value["warnings"])
    tuples = {(x["kind"], x["recordId"], x["category"]) for x in value["warnings"]}
    coverage_rows = {x["id"]: x for x in coverage["items"]}
    for warning in value["warnings"]:
        if warning["kind"] == "MandatoryReviewIncomplete":
            linked = findings.get(warning["recordId"])
            require(linked is not None and linked["category"] == warning["category"], "review-warning-link")
        elif warning["kind"] == "CoverageIncomplete":
            linked = coverage_rows.get(warning["recordId"])
            require(linked is not None and linked["category"] == warning["category"], "coverage-warning-link")
    for row in value["findings"]:
        if row["severity"] in {"Critical", "High"} and row["state"] in {"Proposed", "AutoConfirmed"}:
            require(row["mandatoryReview"], "mandatory-review-required")
            require(("MandatoryReviewIncomplete", row["id"], row["category"]) in tuples, "missing-review-warning")
    for row in coverage["items"]:
        if row["state"] not in {"Pass", "Finding", "NotApplicable"}:
            require(("CoverageIncomplete", row["id"], row["category"]) in tuples, "missing-coverage-warning")
    closed(value["technicalAppendices"], ["notes", "protectedReferences"])
    display(value["technicalAppendices"]["notes"])
    references = value["technicalAppendices"]["protectedReferences"]
    sorted_set(references, lambda x: x["id"])
    for row in references:
        closed(row, ["id", "category", "availability", "reason"])
        uuid(row["id"])
        token(row["category"])
        enum(row["availability"], {"Available", "Unavailable", "Redacted"})
        enum(row["reason"], REASONS)
        require((row["availability"] == "Available") == (row["reason"] == "None"), "reference-reason")
        if row["availability"] == "Redacted":
            require(row["reason"] == "Redacted", "reference-redacted")
    reference_ids = {x["id"] for x in references}
    require(all(set(row["referenceIds"]) <= reference_ids for row in value["findings"]), "reference-link")
    record_sections = {name: value[name] for name in ["executiveSummary", "environmentScope", "dimensions",
                       "findings", "rootCauses", "healthyControls", "recommendations", "acceptedRisks", "methodology"]}
    record_sections.update({"coverage": coverage["items"], "technicalAppendices.notes": value["technicalAppendices"]["notes"],
                            "technicalAppendices.protectedReferences": references})
    sorted_set(value["redactionMarkers"], lambda x: (x["section"], x["id"], x["field"], x["reason"]))
    for row in value["redactionMarkers"]:
        closed(row, ["section", "id", "field", "reason"])
        require(row["section"] in record_sections, "redaction-section")
        uuid(row["id"])
        token(row["field"])
        linked = {x["id"]: x for x in record_sections[row["section"]]}.get(row["id"])
        require(linked is not None and row["field"] in linked, "redaction-record-field")
        enum(row["reason"], REASONS - {"None", "NotApplicable"})


def source(value):
    closed(value, SOURCE_FIELDS)
    scope(value["scope"])
    uuid(value["runId"])
    counter(value["runRevision"], True)
    enum(value["assessmentState"], {"Completed", "CompletedWithGaps"})
    inputs(value["inputs"])
    projection(value["projection"])
    score(value["score"])
    warnings(value["warnings"])
    retention(value["retention"])
    provenance(value["provenance"])
    sorted_set(value["requiredCategories"])
    for member in value["requiredCategories"]:
        token(member)
    field_set(value["requiredFields"])
    p = value["projection"]
    require(p["scores"] == value["score"] and p["warnings"] == value["warnings"] and p["inputs"] == value["inputs"], "source-repetition")
    require(p["runId"] == value["runId"] and p["runRevision"] == value["runRevision"], "run-binding")
    require(p["assessmentId"] == value["scope"]["assessmentId"] and p["scope"] == {k: v for k, v in value["scope"].items() if k != "assessmentId"}, "source-scope")
    def visit(node):
        if type(node) is dict:
            if "category" in node:
                require(node["category"] in value["requiredCategories"], "category-binding")
            for child in node.values():
                visit(child)
        elif type(node) is list:
            for child in node:
                visit(child)
    visit(p)
    provenance_ids = {row["opaqueRecordId"] for row in value["provenance"]}
    for warning in value["warnings"]:
        if warning["kind"] == "SourceLimitation":
            require(warning["recordId"] in provenance_ids and warning["category"] in value["requiredCategories"], "source-limitation-provenance")
    for row in p["recommendations"]:
        for name in ["priority", "effort"]:
            if row[name]["availability"] == "Available":
                require(row[name]["sourceReference"] in provenance_ids, "estimate-provenance")


def command(value):
    closed(value, COMMAND_FIELDS)
    actor(value["actor"])
    scope(value["scope"])
    uuid(value["operationId"])
    uuid(value["runId"])
    counter(value["expectedRunRevision"], True)
    digest(value["expectedSourceDigest"])
    warnings(value["acknowledgedWarnings"])


def event(value):
    closed(value, EVENT_FIELDS)
    require(value["schemaVersion"] == "report-publication-audit-v1", "event-version")
    for name in ["eventId", "streamId", "writerBindingReference", "invocationId", "correlationId"]:
        uuid(value[name])
    counter(value["sequence"], True)
    digest(value["previousEventDigest"])
    utc(value["eventAtUtc"])
    enum(value["actorKind"], {"Human", "Anonymous"})
    if value["actorKind"] == "Human":
        actor(value["actor"])
    else:
        require(value["actor"] is None, "anonymous-actor")
    if value["scope"] is None:
        require(value["resourceKind"] is None and value["resourceId"] is None, "unresolved-resource")
    else:
        scope(value["scope"])
        enum(value["resourceKind"], {"Run", "ReportVersion"})
        uuid(value["resourceId"])
    enum(value["action"], {"Publish", "ReadExact"})
    enum(value["outcome"], {"Succeeded", "Denied", "Failed", "Cancelled"})
    reasons = {"Succeeded": {"None"}, "Denied": {"InvalidInput", "AuthorityDenied", "SourceUnavailable", "RevisionConflict", "IdempotencyConflict", "LifecycleDenied"},
               "Failed": {"IntegrityMismatch", "DependencyUnavailable"}, "Cancelled": {"Cancelled"}}
    enum(value["reason"], reasons[value["outcome"]])
    if value["action"] == "Publish":
        uuid(value["operationId"])
    else:
        require(value["operationId"] is None, "read-operation")
    if value["outcome"] == "Succeeded":
        digest(value["manifestDigest"])
        require(value["actorKind"] == "Human" and value["scope"] is not None, "success-authority")
        require(value["resourceKind"] == ("Run" if value["action"] == "Publish" else "ReportVersion"), "success-resource")
    else:
        require(value["manifestDigest"] is None, "failure-manifest")
    require(value["redactedFields"] == [], "full-reader-redaction")
    require(value["returnedFields"] == (FIELDS if value["action"] == "ReadExact" and value["outcome"] == "Succeeded" else []), "returned-fields")


def manifest(value):
    closed(value, MANIFEST_FIELDS)
    require(value["schemaVersion"] == "health-report-manifest-v1" and value["projectionSchemaVersion"] == "health-report-projection-v1" and value["scoreSchemaVersion"] == "health-report-score-v1", "manifest-version")
    scope(value["scope"], False)
    for name in ["assessmentId", "reportVersionId", "runId"]:
        uuid(value[name])
    counter(value["runRevision"], True)
    utc(value["createdAtUtc"])
    closed(value["createdBy"], ["tenantId", "objectId"])
    for member in value["createdBy"].values():
        uuid(member)
    enum(value["assessmentState"], {"Completed", "CompletedWithGaps"})
    enum(value["approvalState"], {"Published", "PublishedWithWarnings"})
    inputs(value["inputs"])
    for name in ["projectionDigest", "scoreDigest", "sourceDigest"]:
        digest(value[name])
    sorted_set(value["requiredCategories"])
    for member in value["requiredCategories"]:
        token(member)
    field_set(value["requiredFields"])
    require(value["classification"] == "MinimizedDerivedReport", "classification")
    retention(value["retention"])
    provenance(value["provenance"])
    sorted_set(value["artifactInputs"], lambda x: x["kind"])
    require([x["kind"] for x in value["artifactInputs"]] == ["Projection", "Score"], "artifact-kinds")
    for row in value["artifactInputs"]:
        closed(row, ["kind", "digest", "byteLength"])
        digest(row["digest"])
        counter(row["byteLength"], True)


def receipt(value):
    closed(value, RECEIPT_FIELDS)
    require(value["schemaVersion"] == "report-publication-receipt-v1", "receipt-version")
    actor(value["actor"])
    scope(value["scope"])
    for name in ["operationId", "runId", "reportVersionId", "eventId"]:
        uuid(value[name])
    counter(value["expectedRunRevision"], True)
    for name in ["commandDigest", "manifestDigest", "projectionDigest", "scoreDigest", "eventDigest"]:
        digest(value[name])
    utc(value["committedAtUtc"])


def read_request(value):
    closed(value, READ_FIELDS)
    require(value["schemaVersion"] == "report-exact-read-request-v1", "read-version")
    actor(value["actor"])
    scope(value["scope"])
    uuid(value["reportVersionId"])
    uuid(value["invocationId"])
    digest(value["expectedManifestDigest"])


def read_receipt(value):
    closed(value, READ_RECEIPT_FIELDS)
    require(value["schemaVersion"] == "report-exact-read-receipt-v1", "read-receipt-version")
    actor(value["actor"])
    scope(value["scope"])
    for name in ["invocationId", "reportVersionId", "eventId"]:
        uuid(value[name])
    for name in ["requestDigest", "manifestDigest", "eventDigest"]:
        digest(value[name])
    utc(value["committedAtUtc"])


def object_pairs(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate-field")
        result[key] = value
    return result


def loaded(data):
    require(not data.startswith(b"\xef\xbb\xbf"), "bom")
    return json.loads(data.decode("utf-8", errors="strict"), object_pairs_hook=object_pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError("non-finite")))


def canonical(value, depth=0):
    require(depth <= 32, "depth")
    if value is None:
        return b"null"
    if type(value) is bool:
        return b"true" if value else b"false"
    if type(value) is str:
        text(value)
        escapes = {'"': '\\"', '\\': '\\\\', '\b': '\\b', '\f': '\\f', '\n': '\\n', '\r': '\\r', '\t': '\\t'}
        output = '"'
        for character in value:
            output += escapes.get(character, f"\\u{ord(character):04x}" if ord(character) < 32 else character)
        return (output + '"').encode("utf-8", errors="strict")
    if type(value) is list:
        return b"[" + b",".join(canonical(x, depth + 1) for x in value) + b"]"
    require(type(value) is dict, "native-json-number")
    require(all(type(k) is str and k.isascii() for k in value), "ascii-key")
    return b"{" + b",".join(canonical(k, depth + 1) + b":" + canonical(value[k], depth + 1) for k in sorted(value)) + b"}"


def hashed(value):
    return hashlib.sha256(canonical(value)).hexdigest()


VALIDATORS = {"source": source, "projection": projection, "score": score, "command": command,
              "manifest": manifest, "publish-event": event, "read-event": event, "audit": event,
              "receipt": receipt, "read-request": read_request, "read-receipt": read_receipt}


def validate_bytes(filename, data):
    value = loaded(data)
    kind = filename.split(".")[-2]
    VALIDATORS[kind](value)
    require(canonical(value) == data, "original-canonical-bytes")
    return value


def bundle(values, name):
    v = {kind: values[f"{name}.{kind}.json"] for kind in ["source", "projection", "score", "command", "manifest", "publish-event", "receipt", "read-request", "read-event", "read-receipt"]}
    s, p, q, c, m, e, r, request, read, rr = (v[k] for k in v)
    require(s["projection"] == p and s["score"] == q and p["scores"] == q, "frozen-originals")
    require(c["expectedSourceDigest"] == hashed(s) and c["acknowledgedWarnings"] == s["warnings"], "command-source-warning")
    require(c["scope"] == s["scope"] and c["runId"] == s["runId"] and c["expectedRunRevision"] == s["runRevision"], "command-source-binding")
    for digest_name, original in [("sourceDigest", s), ("projectionDigest", p), ("scoreDigest", q)]:
        require(m[digest_name] == hashed(original), "manifest-original-digest")
    for name in ["inputs", "retention", "provenance", "requiredCategories", "requiredFields", "assessmentState"]:
        require(m[name] == s[name], "manifest-source-binding")
    require(m["approvalState"] == ("PublishedWithWarnings" if s["warnings"] else "Published"), "warning-approval")
    require(m["redactionMarkers"] == p["redactionMarkers"], "redaction-original")
    for descriptor, original in zip(m["artifactInputs"], [p, q]):
        require(descriptor["digest"] == hashed(original) and int(descriptor["byteLength"]) == len(canonical(original)), "artifact-original")
    require(r["commandDigest"] == hashed(c) and r["eventDigest"] == hashed(e) and r["manifestDigest"] == hashed(m), "receipt-originals")
    require(r["actor"] == c["actor"] == e["actor"] and r["scope"] == c["scope"] == e["scope"], "receipt-actor-scope")
    require(r["operationId"] == c["operationId"] == e["operationId"] and r["eventId"] == e["eventId"], "receipt-event-binding")
    require(r["runId"] == e["resourceId"] == c["runId"] and r["expectedRunRevision"] == c["expectedRunRevision"], "receipt-run")
    require(r["reportVersionId"] == m["reportVersionId"] and r["projectionDigest"] == m["projectionDigest"] and r["scoreDigest"] == m["scoreDigest"], "receipt-version-digests")
    require(e["manifestDigest"] == hashed(m) and r["committedAtUtc"] == e["eventAtUtc"], "event-time-manifest")
    require(rr["requestDigest"] == hashed(request) and rr["eventDigest"] == hashed(read), "read-receipt-originals")
    require(request["expectedManifestDigest"] == rr["manifestDigest"] == read["manifestDigest"] == hashed(m), "read-original-manifest")
    require(request["reportVersionId"] == rr["reportVersionId"] == read["resourceId"] == m["reportVersionId"], "read-version")
    require(request["actor"] == rr["actor"] == read["actor"] and request["scope"] == rr["scope"] == read["scope"], "read-actor-scope")
    require(request["invocationId"] == rr["invocationId"] == read["invocationId"] and rr["eventId"] == read["eventId"], "read-invocation-event")
    require(read["previousEventDigest"] == hashed(e) and int(read["sequence"]) == int(e["sequence"]) + 1 and read["streamId"] == e["streamId"], "stream-chain")
    require(rr["committedAtUtc"] == read["eventAtUtc"], "read-commit-time")


def verify():
    goldens = json.loads((FIXTURES / "goldens.json").read_text(encoding="utf-8"))
    require(goldens["schemaVersion"] == "independent-native-publication-goldens-v1", "golden-version")
    values = {}
    for filename, commitment in goldens["files"].items():
        data = (FIXTURES / filename).read_bytes()
        require(len(data) == commitment["byteLength"] and hashlib.sha256(data).hexdigest() == commitment["sha256"], "literal-golden-commitment")
        values[filename] = validate_bytes(filename, data)
    bundle(values, "completed")
    bundle(values, "warned")
    bundle(values, "source-limitation")
    return values


if __name__ == "__main__":
    verified = verify()
    print(f"PASS {len(verified)} original canonical vectors; three publication/read chains and anonymous denial.")

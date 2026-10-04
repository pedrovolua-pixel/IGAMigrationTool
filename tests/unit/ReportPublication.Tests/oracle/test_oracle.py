#!/usr/bin/env python3
"""Independent adversarial verification of original fixture commitments.

These reference cases validate the contract/oracle only. They do not claim a
production publisher, persisted fence, authorization or delivery test passed.
"""
import copy
import importlib.util
import json
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("native_reference", Path(__file__).with_name("verify_vectors.py"))
reference = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reference)


class ContractOracleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = reference.verify()

    def deny(self, function, value):
        with self.assertRaises(ValueError):
            function(value)

    def mutate(self, filename, path, value):
        mutant = copy.deepcopy(self.original[filename])
        cursor = mutant
        for part in path[:-1]:
            cursor = cursor[part]
        cursor[path[-1]] = value
        return mutant

    def test_original_commitments_and_distinct_source_command_manifest(self):
        self.assertEqual(len(self.original), 31)
        for name in ["completed", "warned", "source-limitation"]:
            source = self.original[f"{name}.source.json"]
            command = self.original[f"{name}.command.json"]
            manifest = self.original[f"{name}.manifest.json"]
            self.assertEqual(len({reference.hashed(source), reference.hashed(command), reference.hashed(manifest)}), 3)

    def test_unknown_missing_and_numeric_fields_refuse(self):
        source = copy.deepcopy(self.original["completed.source.json"])
        source["unexpected"] = "must-refuse"
        self.deny(reference.source, source)
        source.pop("unexpected")
        source.pop("retention")
        self.deny(reference.source, source)
        for value in [9, True, "0", "09", "+9", "9223372036854775808"]:
            with self.subTest(value=value):
                self.deny(reference.source, self.mutate("completed.source.json", ["runRevision"], value))

    def test_noncanonical_decimal_forms_refuse(self):
        for value in [72.5, "072.5", "72.50", "+72.5", "-0", "1e2", "100.0001", "0.12345678901234567890123456789"]:
            with self.subTest(value=value):
                self.deny(reference.score, self.mutate("completed.score.json", ["quality", "value"], value))

    def test_unavailable_values_not_substituted(self):
        unavailable = self.mutate("completed.score.json", ["quality", "availability"], "Unavailable")
        self.deny(reference.score, unavailable)
        unavailable["quality"].update(value=None, reason="NotAssessed")
        reference.score(unavailable)
        self.deny(reference.score, self.mutate("completed.score.json", ["quality", "value"], None))

    def test_source_states_are_terminal_without_gap_iff(self):
        source = self.original["source-limitation.source.json"]
        self.assertEqual(source["assessmentState"], "CompletedWithGaps")
        self.assertEqual(source["projection"]["coverage"]["counts"]["gap"], "0")
        reference.source(source)
        for state in ["Scoring", "SyntheticApproved", "Running", "Cancelled", "Failed", "completed", "syn-published"]:
            with self.subTest(state=state):
                self.deny(reference.source, self.mutate("completed.source.json", ["assessmentState"], state))

    def test_critical_high_flag_false_never_bypasses_warning(self):
        for severity in ["Critical", "High"]:
            for state in ["Proposed", "AutoConfirmed"]:
                for warnings_present in [True, False]:
                    with self.subTest(severity=severity, state=state, warnings=warnings_present):
                        projection = copy.deepcopy(self.original["warned.projection.json"])
                        projection["findings"][0].update(severity=severity, state=state, mandatoryReview=False)
                        if not warnings_present:
                            projection["warnings"] = [x for x in projection["warnings"] if x["kind"] != "MandatoryReviewIncomplete"]
                        self.deny(reference.projection, projection)
        missing = self.mutate("warned.projection.json", ["warnings"], [])
        self.deny(reference.projection, missing)

    def test_exact_warning_acknowledgment_link(self):
        changed = copy.deepcopy(self.original)
        changed["warned.command.json"]["acknowledgedWarnings"] = []
        self.deny(lambda x: reference.bundle(x, "warned"), changed)
        changed = copy.deepcopy(self.original)
        changed["completed.command.json"]["acknowledgedWarnings"] = copy.deepcopy(self.original["warned.command.json"]["acknowledgedWarnings"])
        self.deny(lambda x: reference.bundle(x, "completed"), changed)

    def test_repeated_scope_input_score_and_original_digest_refuse(self):
        for path, value in [(["scope", "assessmentId"], "00000000-0000-4000-8000-000000009999"),
                            (["inputs", "baselineDigest"], "ff" * 32),
                            (["projection", "scores", "provisional", "value"], "73"),
                            (["projection", "runRevision"], "10")]:
            with self.subTest(path=path):
                self.deny(reference.source, self.mutate("completed.source.json", path, value))
        changed = copy.deepcopy(self.original)
        changed["completed.manifest.json"]["sourceDigest"] = "ff" * 32
        self.deny(lambda x: reference.bundle(x, "completed"), changed)

    def test_root_reference_category_linkage_and_field_registry(self):
        cases = [(["findings", 0, "rootCauseIds"], []),
                 (["rootCauses", 0, "findingIds"], []),
                 (["findings", 0, "referenceIds"], ["00000000-0000-4000-8000-000000009999"]),
                 (["recommendations", 0, "findingId"], "00000000-0000-4000-8000-000000009999")]
        for path, value in cases:
            with self.subTest(path=path):
                self.deny(reference.projection, self.mutate("completed.projection.json", path, value))
        self.deny(reference.source, self.mutate("completed.source.json", ["requiredCategories"], ["ungranted"]))
        for fields in [reference.FIELDS[:-1], reference.FIELDS + ["rawEvidence"], ["*"]]:
            self.deny(reference.field_set, fields)

    def test_warnings_match_exact_kind_record_and_category(self):
        for path, value in [(["warnings", 0, "category"], "wrongcategory"),
                            (["warnings", 1, "category"], "wrongcategory"),
                            (["warnings", 0, "recordId"], self.original["warned.projection.json"]["findings"][0]["id"]),
                            (["warnings", 1, "recordId"], self.original["warned.projection.json"]["coverage"]["items"][1]["id"])]:
            self.deny(reference.projection, self.mutate("warned.projection.json", path, value))
        source = self.mutate("source-limitation.source.json", ["provenance"], [])
        self.deny(reference.source, source)

    def test_marker_section_id_and_field_are_exact(self):
        projection = copy.deepcopy(self.original["warned.projection.json"])
        projection["redactionMarkers"] = [{"section": "findings", "id": projection["healthyControls"][0]["id"],
                                           "field": "summary", "reason": "Redacted"}]
        self.deny(reference.projection, projection)
        for section in ["inputs", "scores", "maturity", "warnings", "redactionMarkers"]:
            self.deny(reference.projection, self.mutate("warned.projection.json", ["redactionMarkers", 0, "section"], section))
        self.deny(reference.projection, self.mutate("warned.projection.json", ["redactionMarkers", 0, "field"], "inventedField"))

    def test_coverage_reason_arithmetic_and_duplicate_ids(self):
        for reason in ["Unsupported", "None", "Deleted"]:
            self.deny(reference.projection, self.mutate("warned.projection.json", ["coverage", "items", 1, "reason"], reason))
        self.deny(reference.projection, self.mutate("completed.projection.json", ["coverage", "counts", "planned"], "2"))
        projection = copy.deepcopy(self.original["completed.projection.json"])
        projection["coverage"]["items"].append(copy.deepcopy(projection["coverage"]["items"][0]))
        self.deny(reference.projection, projection)

    def test_estimates_need_source_provenance_and_confidence_availability(self):
        source = copy.deepcopy(self.original["completed.source.json"])
        source["projection"]["recommendations"][0]["priority"] = {
            "availability": "Available", "value": "First", "reason": "None",
            "sourceReference": "00000000-0000-4000-8000-000000009999"}
        self.deny(reference.source, source)
        for path, value in [(["findings", 0, "confidencePercent"], "100.1"),
                            (["findings", 0, "confidenceAvailability"], "Unavailable"),
                            (["findings", 0, "confidenceBand"], None)]:
            self.deny(reference.projection, self.mutate("completed.projection.json", path, value))

    def test_receipt_actor_version_event_digest_and_invocation_substitution(self):
        for file, path, value in [
            ("completed.receipt.json", ["actor", "sessionId"], "00000000-0000-4000-8000-000000009999"),
            ("completed.receipt.json", ["eventDigest"], "ff" * 32),
            ("completed.read-receipt.json", ["invocationId"], "00000000-0000-4000-8000-000000009999"),
            ("completed.read-receipt.json", ["reportVersionId"], "00000000-0000-4000-8000-000000009999"),
            ("completed.read-event.json", ["previousEventDigest"], "ff" * 32),
        ]:
            changed = copy.deepcopy(self.original)
            changed[file] = self.mutate(file, path, value)
            self.deny(lambda x: reference.bundle(x, "completed"), changed)

    def test_audit_null_enum_pairs_no_payload_and_no_fictional_outcome(self):
        for path, value in [(["outcome"], "Failed"), (["manifestDigest"], "ff" * 32),
                            (["scope"], self.original["completed.source.json"]["scope"]),
                            (["actor"], self.original["completed.command.json"]["actor"]),
                            (["reason"], "details must never be echoed")]:
            self.deny(reference.event, self.mutate("anonymous-denial.audit.json", path, value))
        event = copy.deepcopy(self.original["completed.publish-event.json"])
        event["reportText"] = "PROHIBITED_REPORT_SENTINEL"
        self.deny(reference.event, event)
        for outcome, reason in [("Failed", "IntegrityMismatch"), ("Failed", "DependencyUnavailable"), ("Cancelled", "Cancelled")]:
            event = copy.deepcopy(self.original["anonymous-denial.audit.json"])
            event.update(outcome=outcome, reason=reason)
            reference.event(event)
        self.deny(reference.event, self.mutate("anonymous-denial.audit.json", ["outcome"], "CommitOutcomeUnknown"))

    def test_command_excludes_invocation_and_correlation_exactly(self):
        command = self.original["completed.command.json"]
        self.assertNotIn("invocationId", command)
        self.assertNotIn("correlationId", command)
        for key in ["invocationId", "correlationId", "schemaVersion"]:
            value = copy.deepcopy(command)
            value[key] = "00000000-0000-4000-8000-000000009999"
            self.deny(reference.command, value)
        changed = copy.deepcopy(command)
        changed["actor"]["securityVersion"] = "4"
        reference.command(changed)
        self.assertNotEqual(reference.hashed(command), reference.hashed(changed))

    def test_original_bytes_refuse_whitespace_key_order_bom_and_trailing_newline(self):
        filename = "completed.score.json"
        raw = (reference.FIXTURES / filename).read_bytes()
        for data in [b"\xef\xbb\xbf" + raw, raw + b"\n", b" " + raw,
                     json.dumps(dict(reversed(list(self.original[filename].items()))), ensure_ascii=False,
                                separators=(",", ":")).encode("utf-8")]:
            self.deny(lambda x: reference.validate_bytes(filename, x), data)
        self.deny(reference.loaded, b'{"x":"a","x":"b"}')
        self.deny(reference.loaded, b'{"x":{"k":"a","k":"b"}}')

    def test_unicode_raw_vectors_and_original_nonascii_controls(self):
        records = json.loads((reference.FIXTURES / "unicode-vectors.json").read_text(encoding="utf-8"))
        for row in records:
            with self.subTest(id=row["id"]):
                data = bytes.fromhex(row["hex"])
                if row["expected"] == "Reject":
                    self.deny(lambda x: reference.canonical(reference.loaded(x)), data)
                else:
                    result = reference.canonical(reference.loaded(data))
                    self.assertEqual(result.hex(), row["canonicalHex"])
        text = self.original["completed.projection.json"]["executiveSummary"][0]["text"]
        self.assertIn("café", text)
        self.assertIn("👩🏽‍💻", text)
        self.assertIn("\x00", text)
        self.assertIn("\x1f", text)
        self.assertIn("\n", text)
        self.assertIn("\t", text)
        self.assertNotEqual(reference.canonical("é"), reference.canonical("e\u0301"))

    def test_scalar_uuid_time_token_and_depth_boundaries(self):
        for value in ["00000000-0000-0000-0000-000000000000", "00000000-0000-4000-8000-00000000000A", "not-an-id"]:
            self.deny(reference.uuid, value)
        for value in ["2026-02-30T00:00:00.0000000Z", "2026-10-03T12:00:00Z", "2026-10-03T12:00:00.0000000+00:00"]:
            self.deny(reference.utc, value)
        for value in ["", "a" * 129, "with space", "é", "["]:
            self.deny(reference.token, value)
        reference.counter("9223372036854775807")
        self.deny(reference.counter, "9223372036854775808")
        reference.text("🚀" * 4096)
        self.deny(reference.text, "🚀" * 4097)
        self.deny(reference.canonical, 1)
        nested = None
        for _ in range(34):
            nested = [nested]
        self.deny(reference.canonical, nested)


if __name__ == "__main__":
    unittest.main(verbosity=2)

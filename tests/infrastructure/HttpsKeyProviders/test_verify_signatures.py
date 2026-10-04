"""Safety cases with mocked subprocesses ONLY; these never verify real signatures."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

SPEC = importlib.util.spec_from_file_location("signature_verifier", Path(__file__).with_name("verify-signatures.py"))
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)
REAL_MANIFEST = v.MANIFEST_PATH.read_bytes()
REAL_SHA = v.MANIFEST_SHA256


class SafetyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="iga_signature_mock_")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.archives = self.root / "archives with spaces"
        self.archives.mkdir()
        self.evidence = self.root / "new evidence"
        self.dotnet = self.root / "dotnet"
        self.dotnet.write_bytes(b"NEVER EXECUTED; subprocess mock only")
        self.dotnet.chmod(0o700)
        self.manifest = json.loads(REAL_MANIFEST)
        for p in self.manifest["packages"]:
            raw = ("synthetic not a package: " + p["fileName"]).encode()
            (self.archives / p["fileName"]).write_bytes(raw)
            p["archiveSHA256"] = hashlib.sha256(raw).hexdigest()
        self.manifest_path = self.root / "manifest.json"
        self.path_patch = patch.object(v, "MANIFEST_PATH", self.manifest_path)
        self.path_patch.start()
        self.addCleanup(self.path_patch.stop)
        self.hash_patch = patch.object(v, "MANIFEST_SHA256", "temporary")
        self.hash_patch.start()
        self.addCleanup(self.hash_patch.stop)
        self.bind_manifest(self.manifest)
        self.run = Mock(side_effect=self.success)

    def bind_raw(self, raw):
        self.manifest_path.write_bytes(raw)
        v.MANIFEST_SHA256 = v.sha256_bytes(raw)

    def bind_manifest(self, manifest):
        self.bind_raw((json.dumps(manifest) + "\n").encode())

    def success(self, argv, **kwargs):
        self.assertFalse(kwargs["shell"])
        self.assertTrue(kwargs["capture_output"])
        self.assertFalse(kwargs["check"])
        self.assertEqual(kwargs["timeout"], v.TIMEOUT_SECONDS)
        if argv[1:] == ["--version"]:
            return subprocess.CompletedProcess(argv, 0, b"10.0.401\n", b"")
        return subprocess.CompletedProcess(argv, 0, b"mock only: NOT real verification\xff", b"mock stderr\n")

    def invoke(self, **overrides):
        args = dict(run=self.run, system="Linux", env={})
        args.update(overrides)
        return v.verify(self.archives, self.dotnet, self.evidence, **args)

    def receipt(self):
        return json.loads((self.evidence / "receipt.json").read_text())

    def denied_before_process(self):
        with self.assertRaises((v.Denied, OSError, ValueError)):
            self.invoke()
        self.run.assert_not_called()
        self.assertFalse(self.evidence.exists())

    def test_committed_manifest_hash_and_binding(self):
        self.assertEqual(v.sha256_bytes(REAL_MANIFEST), REAL_SHA)
        with patch.object(v, "MANIFEST_PATH", Path(__file__).with_name("resolved-37-archives.json")), patch.object(v, "MANIFEST_SHA256", REAL_SHA):
            m = v.load_manifest()
        self.assertEqual(len(m["packages"]), 37)
        self.assertEqual(m["sourceCommit"], v.SOURCE)
        self.assertEqual(m["experimentalLockSHA256"], v.LOCK)

    def test_unsupported_platform_zero_subprocesses(self):
        for system in ("Darwin", "", "FreeBSD", "linux"):
            with self.subTest(system=system), self.assertRaises(v.Denied):
                self.invoke(system=system)
        self.run.assert_not_called()
        self.assertFalse(self.evidence.exists())

    def test_cli_macos_denies_without_subprocess(self):
        argv = ["verify-signatures.py", "--archives", str(self.archives),
                "--dotnet", str(self.dotnet), "--evidence", str(self.evidence)]
        with patch.object(v.sys, "argv", argv), patch.object(v.platform, "system", return_value="Darwin"), \
                patch.object(v.subprocess, "run", self.run), patch.object(v.sys, "stderr", io.StringIO()):
            self.assertEqual(v.main(), 1)
        self.run.assert_not_called()
        self.assertFalse(self.evidence.exists())

    def test_environment_overrides_presence_even_empty_denies(self):
        for name in sorted(v.FORBIDDEN_ENV) + ["DOTNET_ROLL_FORWARD", "dotnet_sdk_roll_forward", "CORECLR_PROFILER_PATH"]:
            with self.subTest(name=name), self.assertRaises(v.Denied):
                self.invoke(env={name: ""})
        self.run.assert_not_called()

    def test_env_error_never_prints_value(self):
        with self.assertRaises(v.Denied) as caught:
            self.invoke(env={"NUGET_CERT_REVOCATION_MODE": "synthetic-private-value"})
        self.assertNotIn("synthetic-private-value", str(caught.exception))

    def test_tampered_manifest_pin_denies(self):
        self.manifest_path.write_bytes(self.manifest_path.read_bytes() + b" ")
        self.denied_before_process()

    def test_closed_manifest_rejects_missing_unknown_duplicate_keys(self):
        for field in ("sdkVersion", "packages"):
            m = copy.deepcopy(self.manifest)
            del m[field]
            self.bind_manifest(m)
            self.denied_before_process()
        m = copy.deepcopy(self.manifest)
        m["unknown"] = 1
        self.bind_manifest(m)
        self.denied_before_process()
        raw = self.manifest_path.read_bytes().replace(b'{', b'{"schemaVersion":1,', 1)
        self.bind_raw(raw)
        self.denied_before_process()

    def test_closed_package_metadata_and_binding(self):
        mutations = [("id", "../bad"), ("version", "1.0.0-preview"),
                     ("fileName", "../archive.nupkg"), ("archiveSHA256", "A" * 64),
                     ("version", 1)]
        for field, value in mutations:
            m = copy.deepcopy(self.manifest)
            m["packages"][0][field] = value
            self.bind_manifest(m)
            self.denied_before_process()
        for field, value in (("schemaVersion", True), ("sdkVersion", "10.0.402"),
                             ("sourceCommit", "0" * 40), ("experimentalLockSHA256", "0" * 64),
                             ("graphKind", "production-lock")):
            m = copy.deepcopy(self.manifest)
            m[field] = value
            self.bind_manifest(m)
            self.denied_before_process()
        for change in ("extra", "missing"):
            m = copy.deepcopy(self.manifest)
            if change == "extra":
                m["packages"][0]["unknown"] = "x"
            else:
                del m["packages"][0]["archiveSHA256"]
            self.bind_manifest(m)
            self.denied_before_process()

    def test_manifest_wrong_count_duplicate_package_denies(self):
        for count in (36, 38):
            m = copy.deepcopy(self.manifest)
            m["packages"] = (m["packages"] * 2)[:count]
            self.bind_manifest(m)
            self.denied_before_process()
        m = copy.deepcopy(self.manifest)
        m["packages"][1] = m["packages"][0]
        self.bind_manifest(m)
        self.denied_before_process()

    def test_inventory_missing_extra_nested_and_symlink_denies(self):
        first = self.archives / self.manifest["packages"][0]["fileName"]
        raw = first.read_bytes()
        first.unlink()
        self.denied_before_process()
        first.write_bytes(raw)
        extra = self.archives / "unexpected.nupkg"
        extra.write_bytes(b"extra")
        self.denied_before_process()
        extra.unlink()
        extra.mkdir()
        self.denied_before_process()
        extra.rmdir()
        first.unlink()
        target = self.root / "outside"
        target.write_bytes(raw)
        first.symlink_to(target)
        self.denied_before_process()

    def test_directory_symlink_and_corrupt_archive_denies(self):
        alias = self.root / "alias"
        alias.symlink_to(self.archives, target_is_directory=True)
        with self.assertRaises(v.Denied):
            v.verify(alias, self.dotnet, self.evidence, run=self.run, system="Linux", env={})
        (self.archives / self.manifest["packages"][0]["fileName"]).write_bytes(b"changed")
        self.denied_before_process()

    def test_dotnet_relative_wrapper_missing_and_existing_evidence_denies(self):
        for exe in (Path("dotnet"), self.root / "missing", self.root / "dotnet.cmd"):
            if exe.name == "dotnet.cmd":
                exe.write_bytes(b"not executable native binary")
                exe.chmod(0o700)
            with self.assertRaises(v.Denied):
                v.verify(self.archives, exe, self.evidence, run=self.run, system="Linux", env={})
        self.evidence.mkdir()
        with self.assertRaises(v.Denied):
            self.invoke()
        self.run.assert_not_called()

    def test_wrong_sdk_nonzero_launch_timeout_abort_before_packages(self):
        outcomes = [subprocess.CompletedProcess([], 0, b"10.0.402\n", b""),
                    subprocess.CompletedProcess([], 1, b"10.0.401\n", b"failure"),
                    subprocess.TimeoutExpired([], 180, output=b"partial", stderr=b"timeout"),
                    OSError("synthetic launch failure")]
        for index, outcome in enumerate(outcomes):
            self.evidence = self.root / ("sdk-case-" + str(index))
            self.run = Mock(side_effect=outcome) if isinstance(outcome, Exception) else Mock(return_value=outcome)
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(self.run.call_count, 1)
            r = self.receipt()
            self.assertEqual(r["status"], "FAILED")
            self.assertEqual(r["packages"], [])

    def test_exact_argv_config_pin_and_raw_receipts_mocked_pass_only(self):
        self.assertEqual(self.invoke(env={"DOTNET_CLI_TELEMETRY_OPTOUT": "1"}), 0)
        self.assertEqual(self.run.call_count, 38)
        r = self.receipt()
        self.assertEqual(r["status"], "PASS")  # Mocks only, never integrity evidence.
        self.assertEqual(len(r["packages"]), 37)
        self.assertEqual((self.evidence / "nuget-verification.config").read_text(), '<?xml version="1.0" encoding="utf-8"?>\n<configuration />\n')
        self.assertEqual(json.loads((self.evidence / "global.json").read_text()),
                         {"sdk": {"version": "10.0.401", "rollForward": "disable", "allowPrerelease": False}})
        for call, p in zip(self.run.call_args_list[1:], self.manifest["packages"]):
            self.assertEqual(call.args[0], [str(self.dotnet), "nuget", "verify",
                             str(self.evidence.resolve() / "archives" / p["fileName"]),
                             "--all", "--verbosity", "detailed", "--configfile",
                             str(self.evidence.resolve() / "nuget-verification.config")])
        for entry in [r["sdk"]] + r["packages"]:
            for kind in ("stdout", "stderr"):
                raw = (self.evidence / entry[kind]["fileName"]).read_bytes()
                self.assertEqual(entry[kind]["sha256"], v.sha256_bytes(raw))
                self.assertEqual(entry[kind]["bytes"], len(raw))

    def test_windows_native_path_contract_mocked_only(self):
        self.dotnet = self.root / "dotnet.exe"
        self.dotnet.write_bytes(b"mock only")
        self.dotnet.chmod(0o700)
        self.assertEqual(self.invoke(system="Windows"), 0)
        self.assertEqual(self.receipt()["platform"], "Windows")

    def test_package_failure_timeout_launch_continue_exact37_and_no_pass(self):
        for index, failure in enumerate(("exit", "timeout", "launch")):
            self.evidence = self.root / ("package-case-" + str(index))
            calls = []

            def fake(argv, **kwargs):
                calls.append(argv)
                if len(calls) == 2:
                    if failure == "exit":
                        return subprocess.CompletedProcess(argv, 1, b"NU3028", b"ExplicitDistrust")
                    if failure == "timeout":
                        raise subprocess.TimeoutExpired(argv, 180, output=b"partial", stderr=b"timed out")
                    raise OSError("synthetic launch failure")
                return self.success(argv, **kwargs)

            self.run = Mock(side_effect=fake)
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(len(calls), 38)
            r = self.receipt()
            self.assertEqual(len(r["packages"]), 37)
            self.assertEqual(r["status"], "FAILED")
            self.assertEqual(sum(p["exitCode"] == 0 for p in r["packages"]), 36)
            self.assertEqual(r["packages"][0]["status"],
                             {"exit": "EXITED", "timeout": "TIMEOUT", "launch": "LAUNCH_FAILED"}[failure])
            self.assertEqual(r["packages"][0]["exitCode"], 1 if failure == "exit" else None)
            for kind in ("stdout", "stderr"):
                log = r["packages"][0][kind]
                self.assertEqual(log["sha256"], v.sha256_bytes((self.evidence / log["fileName"]).read_bytes()))

    def test_copy_race_zero_subprocesses(self):
        original = v.shutil.copyfile

        def corrupt_copy(src, dst):
            original(src, dst)
            Path(dst).write_bytes(b"changed during copy")

        with patch.object(v.shutil, "copyfile", side_effect=corrupt_copy):
            self.assertEqual(self.invoke(), 1)
        self.run.assert_not_called()
        self.assertEqual(self.receipt()["status"], "FAILED")

    def test_post_verification_snapshot_original_or_config_mutation_denies(self):
        for index, mutation in enumerate(("snapshot", "original", "config", "sdkpin")):
            self.evidence = self.root / ("changed-case-" + str(index))
            calls = []

            def fake(argv, **kwargs):
                calls.append(argv)
                result = self.success(argv, **kwargs)
                if len(calls) == 38:
                    if mutation == "snapshot":
                        Path(argv[3]).write_bytes(b"altered after verification")
                    elif mutation == "original":
                        (self.archives / self.manifest["packages"][0]["fileName"]).write_bytes(b"altered")
                    else:
                        target = "global.json" if mutation == "sdkpin" else "nuget-verification.config"
                        (self.evidence / target).write_bytes(b"altered")
                return result

            self.run = Mock(side_effect=fake)
            self.assertEqual(self.invoke(), 1)
            self.assertEqual(self.receipt()["status"], "FAILED")
            # Restore the synthetic original between subcases.
            for p in self.manifest["packages"]:
                (self.archives / p["fileName"]).write_bytes(("synthetic not a package: " + p["fileName"]).encode())


if __name__ == "__main__":
    unittest.main(verbosity=2)

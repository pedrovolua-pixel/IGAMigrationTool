#!/usr/bin/env python3
"""Prepare normal NuGet signature receipts; never install or accept a package graph."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import subprocess
import sys

MANIFEST_PATH = Path(__file__).with_name("resolved-37-archives.json")
MANIFEST_SHA256 = "8ed2ffab1478d381cf310b62e7c6b198bf47595e8e7100c16d2738fc6a4fee42"
SDK = "10.0.401"
SOURCE = "b186d121419236e15e7fa184a22c6bac4d2db939"
LOCK = "2d726d936d5cb3b5d647ef77a3f62d56e16fb0a70b49738f483ab3c7b5eebe9f"
# This bounds a diagnostic tool subprocess, not a production service deadline.
TIMEOUT_SECONDS = 180
FORBIDDEN_ENV = {
    "NUGET_CERT_REVOCATION_MODE", "DOTNET_NUGET_SIGNATURE_VERIFICATION",
    "NUGET_SIGNATURE_VALIDATION_MODE", "NUGET_NO_WARN", "NUGET_TRUSTED_SIGNERS",
    "SSL_CERT_FILE", "SSL_CERT_DIR", "OPENSSL_CONF", "OPENSSL_MODULES",
    "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE",
    "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR",
    "MSBUILD_SDKS_PATH", "CORECLR_ENABLE_PROFILING", "CORECLR_PROFILER",
    "CORECLR_PROFILER_PATH", "COR_ENABLE_PROFILING", "COR_PROFILER",
    "LD_PRELOAD", "LD_LIBRARY_PATH", "DYLD_INSERT_LIBRARIES",
}


class Denied(ValueError):
    pass


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def regular(path):
    if path.is_symlink() or not stat.S_ISREG(path.lstat().st_mode):
        raise Denied("Expected a regular, non-symlink file: " + path.name)


def digest(path):
    regular(path)
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def closed_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Denied("Duplicate JSON key")
        result[key] = value
    return result


def keys(value, expected):
    if not isinstance(value, dict) or set(value) != set(expected):
        raise Denied("Missing or unknown manifest fields")


def load_manifest():
    regular(MANIFEST_PATH)
    raw = MANIFEST_PATH.read_bytes()
    if sha256_bytes(raw) != MANIFEST_SHA256:
        raise Denied("Committed manifest digest mismatch")
    m = json.loads(raw, object_pairs_hook=closed_pairs)
    keys(m, ("schemaVersion", "sdkVersion", "graphKind", "sourceCommit",
             "experimentalLockSHA256", "packages"))
    if (type(m["schemaVersion"]) is not int or m["schemaVersion"] != 1
            or m["sdkVersion"] != SDK or m["sourceCommit"] != SOURCE
            or m["experimentalLockSHA256"] != LOCK
            or m["graphKind"] != "experimental-host-graph-not-production-lock"):
        raise Denied("Unapproved manifest binding")
    if not isinstance(m["packages"], list) or len(m["packages"]) != 37:
        raise Denied("Expected exactly 37 packages")
    names = set()
    for p in m["packages"]:
        keys(p, ("id", "version", "fileName", "archiveSHA256"))
        if not all(isinstance(v, str) for v in p.values()):
            raise Denied("Manifest fields must be strings")
        if (not re.fullmatch(r"[A-Za-z0-9]+(?:[.-][A-Za-z0-9]+)*", p["id"])
                or not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", p["version"])
                or p["fileName"] != p["id"].lower() + "." + p["version"] + ".nupkg"
                or not re.fullmatch(r"[0-9a-f]{64}", p["archiveSHA256"])
                or p["fileName"] in names):
            raise Denied("Invalid or duplicate package metadata")
        names.add(p["fileName"])
    return m


def environment_check(env):
    denied = sorted(k for k in env if k.upper() in FORBIDDEN_ENV
                    or k.upper().startswith(("DOTNET_ROLL_FORWARD", "DOTNET_SDK_ROLL_FORWARD")))
    if denied:
        # Never print values; do not silently delete overrides.
        raise Denied("Forbidden verification/runtime override variables: " + ", ".join(denied))


def inventory(directory, packages):
    if directory.is_symlink() or not directory.is_dir():
        raise Denied("Archive directory must exist and must not be a symlink")
    actual = list(directory.iterdir())
    if {p.name for p in actual} != {p["fileName"] for p in packages}:
        raise Denied("Archive inventory mismatch; require exact flat 37-file directory")
    for p in packages:
        if digest(directory / p["fileName"]) != p["archiveSHA256"]:
            raise Denied("Archive digest mismatch: " + p["fileName"])


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def capture(argv, cwd, env, label, run):
    entry = {"command": argv, "startedAtUtc": utc_now(), "timeoutSeconds": TIMEOUT_SECONDS}
    try:
        completed = run(argv, cwd=str(cwd), env=env, shell=False,
                        capture_output=True, timeout=TIMEOUT_SECONDS, check=False)
        entry.update(status="EXITED", exitCode=completed.returncode)
        stdout, stderr = completed.stdout, completed.stderr
    except subprocess.TimeoutExpired as error:
        entry.update(status="TIMEOUT", exitCode=None)
        stdout, stderr = error.stdout or b"", error.stderr or b""
    except OSError as error:
        entry.update(status="LAUNCH_FAILED", exitCode=None, errorType=type(error).__name__)
        stdout, stderr = b"", str(error).encode("utf-8")
    if not isinstance(stdout, bytes) or not isinstance(stderr, bytes):
        raise Denied("Subprocess capture must preserve raw bytes")
    for kind, data in (("stdout", stdout), ("stderr", stderr)):
        filename = label + "." + kind + ".log"
        (cwd / filename).write_bytes(data)
        entry[kind] = {"fileName": filename, "sha256": sha256_bytes(data), "bytes": len(data)}
    entry["finishedAtUtc"] = utc_now()
    return entry, stdout


def verify(archives, dotnet, evidence, *, run=None, system=None, env=None):
    # Injection exists only for Python tests; the CLI exposes no simulation/platform option.
    run = subprocess.run if run is None else run
    system = platform.system() if system is None else system
    env = dict(os.environ if env is None else env)
    if system not in ("Linux", "Windows"):
        raise Denied("Unsupported platform; use separately approved Windows/Linux proof")
    environment_check(env)
    manifest = load_manifest()
    packages = manifest["packages"]
    inventory(archives, packages)
    if not dotnet.is_absolute() or not dotnet.is_file():
        raise Denied("Provide a trusted absolute existing dotnet executable")
    expected_name = "dotnet.exe" if system == "Windows" else "dotnet"
    if dotnet.name.lower() != expected_name or not os.access(dotnet, os.X_OK):
        raise Denied("Expected the trusted native dotnet executable, not a shell wrapper")
    if evidence.exists() or evidence.is_symlink() or not evidence.parent.is_dir():
        raise Denied("Evidence destination must be new with an existing parent")
    # mkdir without exist_ok prevents replacing a concurrent or existing receipt directory.
    evidence.mkdir()
    evidence = evidence.resolve()
    receipt = {"schemaVersion": 1, "status": "FAILED", "platform": system,
               "platformRelease": platform.release(), "architecture": platform.machine(),
               "manifestSHA256": MANIFEST_SHA256, "sdkVersionRequired": SDK,
               "sourceCommit": SOURCE, "experimentalLockSHA256": LOCK,
               "graphKind": manifest["graphKind"],
               "startedAtUtc": utc_now(), "packages": [], "sdk": None,
               "meaning": "CLI signature evidence only; not production graph acceptance"}
    try:
        snapshot = evidence / "archives"
        snapshot.mkdir()
        for p in packages:
            shutil.copyfile(archives / p["fileName"], snapshot / p["fileName"])
        inventory(snapshot, packages)
        config = evidence / "nuget-verification.config"
        config.write_text('<?xml version="1.0" encoding="utf-8"?>\n<configuration />\n', encoding="utf-8")
        sdk_pin = evidence / "global.json"
        write_json(sdk_pin, {"sdk": {"version": SDK, "rollForward": "disable", "allowPrerelease": False}})
        receipt["configSHA256"] = digest(config)
        receipt["sdkPinSHA256"] = digest(sdk_pin)
        sdk, output = capture([str(dotnet), "--version"], evidence, env, "sdk-version", run)
        receipt["sdk"] = sdk
        write_json(evidence / "receipt.json", receipt)
        if sdk["exitCode"] != 0 or output.decode("utf-8", errors="strict").strip() != SDK:
            raise Denied("SDK selection failed or does not equal 10.0.401")
        for index, p in enumerate(packages):
            entry = {"id": p["id"], "version": p["version"], "archiveSHA256": p["archiveSHA256"]}
            try:
                if (digest(config) != receipt["configSHA256"]
                        or digest(sdk_pin) != receipt["sdkPinSHA256"]
                        or digest(snapshot / p["fileName"]) != p["archiveSHA256"]):
                    raise Denied("Snapshot or verification config changed")
                argv = [str(dotnet), "nuget", "verify", str(snapshot / p["fileName"]),
                        "--all", "--verbosity", "detailed", "--configfile", str(config)]
                result, _ = capture(argv, evidence, env, "package-{:02d}".format(index + 1), run)
                entry.update(result)
            except (OSError, Denied) as error:
                entry.update(status="PREFLIGHT_FAILED", exitCode=None, error=str(error))
            receipt["packages"].append(entry)
            write_json(evidence / "receipt.json", receipt)
        inventory(snapshot, packages)
        if digest(config) != receipt["configSHA256"] or digest(sdk_pin) != receipt["sdkPinSHA256"]:
            raise Denied("Verification config changed")
        # Also preserve the original preobtained inventory's binding through completion.
        inventory(archives, packages)
        if len(receipt["packages"]) == 37 and all(
                p["status"] == "EXITED" and p["exitCode"] == 0 for p in receipt["packages"]):
            receipt["status"] = "PASS"
    except (OSError, ValueError) as error:
        receipt["failure"] = str(error)
    finally:
        receipt["finishedAtUtc"] = utc_now()
        write_json(evidence / "receipt.json", receipt)
    return 0 if receipt["status"] == "PASS" else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archives", type=Path, required=True)
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    try:
        return verify(args.archives, args.dotnet, args.evidence)
    except (OSError, ValueError) as error:
        print("DENIED: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

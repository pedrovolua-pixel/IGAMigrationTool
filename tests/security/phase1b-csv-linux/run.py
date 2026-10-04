#!/usr/bin/env python3
"""Owned, credential-free synthetic renderer verification; never pulls an image."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
import time
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
LIMITS = {"input": 1048576, "output": 4194304, "stderr": 16384,
          "wallSeconds": 10, "memoryBytes": 402653184, "heapBytes": 268435456,
          "pids": 32, "nanoCpus": 1000000000, "cpuSoftSeconds": 2, "cpuHardSeconds": 3}
ENVIRONMENT = ["DOTNET_EnableDiagnostics=0", "DOTNET_GCHeapHardLimit=10000000",
               "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1", "HOME=/nonexistent", "TMPDIR=/nonexistent"]


def command(args, **kwargs):
    return subprocess.run(list(map(str, args)), check=True, timeout=120,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, **kwargs).stdout


def digest(data):
    return hashlib.sha256(data).hexdigest()


def sealed_publish(dotnet, project, output, extra=()):
    command([dotnet, "restore", project, "--locked-mode", *extra])
    command([dotnet, "publish", project, "--no-restore", "-c", "Release", "--no-self-contained",
             "-p:UseAppHost=false", "-o", output, *extra])
    expected = {"SyntheticCsvRenderer.dll", "SyntheticCsvRenderer.deps.json",
                "SyntheticCsvRenderer.runtimeconfig.json", "SyntheticTaskCsv.dll"}
    for file in output.iterdir():
        if file.name not in expected:
            file.unlink()
    if {file.name for file in output.iterdir()} != expected:
        raise RuntimeError("Published closure differs from the fixed four-file contract")
    return {file.name: digest(file.read_bytes()) for file in sorted(output.iterdir())}


def execute_container(image, payload, mode=None):
    if len(payload) > LIMITS["input"] and mode is not None:
        raise ValueError("Probe input exceeds fixed limit")
    options = ["docker", "create", "--user", "10001:10001", "--read-only", "--network", "none",
               "--ipc", "none", "--cap-drop", "ALL", "--security-opt", "no-new-privileges:true",
               "--security-opt", "seccomp=" + str(HERE / "seccomp.json"), "--memory", "384m",
               "--memory-swap", "384m", "--cpus", "1", "--pids-limit", "32",
               "--ulimit", "cpu=2:3", "--log-driver", "none", "--interactive",
               "--entrypoint", "/usr/bin/env", image, "-i", *ENVIRONMENT,
               "/usr/share/dotnet/dotnet", "/app/SyntheticCsvRenderer.dll"]
    if mode is not None:
        options.append(mode)
    container = command(options).decode().strip()
    result = {"mode": mode or "renderer", "violations": []}
    process = None
    try:
        configuration = json.loads(command(["docker", "inspect", container]))[0]
        host = configuration["HostConfig"]
        required = {"ReadonlyRootfs": True, "NetworkMode": "none", "IpcMode": "none",
                    "Memory": LIMITS["memoryBytes"], "MemorySwap": LIMITS["memoryBytes"],
                    "NanoCpus": LIMITS["nanoCpus"], "PidsLimit": LIMITS["pids"]}
        if any(host.get(key) != value for key, value in required.items()):
            raise RuntimeError("Container effective limits differ from contract")
        if configuration["Config"]["User"] != "10001:10001" or configuration["Mounts"] or host["CapDrop"] != ["ALL"]:
            raise RuntimeError("Container user, capabilities or mounts differ from contract")
        if host["LogConfig"]["Type"] != "none" or not any(item["Name"] == "cpu" and item["Soft"] == 2 and item["Hard"] == 3 for item in host["Ulimits"]):
            raise RuntimeError("Container log/CPU policy differs from contract")
        if "no-new-privileges:true" not in host["SecurityOpt"] or not any(item.startswith("seccomp=") for item in host["SecurityOpt"]):
            raise RuntimeError("Container effective security options differ from contract")
        result["effectiveLimits"] = required
        result["imageId"] = configuration["Image"]
        started = time.monotonic()
        process = subprocess.Popen(["docker", "start", "--attach", "--interactive", container],
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        outputs = {"stdout": bytearray(), "stderr": bytearray()}
        stop = threading.Event()

        def collect(stream, name, maximum):
            while True:
                data = stream.read(8192)
                if not data:
                    break
                outputs[name].extend(data[:max(0, maximum + 1 - len(outputs[name]))])
                if len(outputs[name]) > maximum:
                    result["violations"].append(name)
                    stop.set()
                    break

        def feed():
            try:
                process.stdin.write(payload)
                process.stdin.close()
            except (BrokenPipeError, OSError):
                pass

        threads = [threading.Thread(target=collect, args=(process.stdout, "stdout", LIMITS["output"]), daemon=True),
                   threading.Thread(target=collect, args=(process.stderr, "stderr", LIMITS["stderr"]), daemon=True),
                   threading.Thread(target=feed, daemon=True)]
        for thread in threads:
            thread.start()
        while process.poll() is None:
            if time.monotonic() - started >= LIMITS["wallSeconds"]:
                result["violations"].append("wall")
                stop.set()
            if stop.is_set():
                subprocess.run(["docker", "kill", container], timeout=5, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
                break
            time.sleep(0.02)
        for thread in threads:
            thread.join(timeout=2)
        if any(thread.is_alive() for thread in threads):
            raise RuntimeError("Bounded stream reader did not terminate")
        state = json.loads(command(["docker", "inspect", container]))[0]["State"]
        result.update(exitCode=state["ExitCode"], oomKilled=state["OOMKilled"],
                      elapsedSeconds=round(time.monotonic() - started, 3),
                      stdoutBytes=len(outputs["stdout"]), stderrBytes=len(outputs["stderr"]),
                      stdoutSha256=digest(outputs["stdout"]))
        return result, bytes(outputs["stdout"])
    finally:
        if process is not None and process.poll() is None:
            process.kill()
            process.wait(timeout=5)
        subprocess.run(["docker", "rm", "--force", container], timeout=10,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-only", action="store_true", help="Native benign parity only; never run isolation probes on the host")
    parser.add_argument("--evidence", type=Path, default=HERE / "verification.json")
    args = parser.parse_args()
    project = Path(os.environ.get("Phase1BRendererProject", ROOT / "src/server/workers/SyntheticCsvRenderer/SyntheticCsvRenderer.csproj")).resolve()
    source = project.parents[4]
    fixtures = source / "tests/unit/SyntheticTaskCsv.Tests/Fixtures"
    dotnet = os.environ.get("Phase1BDotnet", "dotnet")
    evidence = {"schema": "phase1b-csv-linux-verification-v1", "linuxStatus": "NOT_EXECUTED",
                "limits": LIMITS, "cases": [], "nativeParity": "NOT_EXECUTED"}
    images = []
    code = 1
    try:
        sdk = command([dotnet, "--version"]).decode().strip()
        if sdk != "10.0.401":
            raise RuntimeError("Pinned SDK 10.0.401 required")
        evidence["sdk"] = sdk
        evidence["sourceCommit"] = command(["git", "-C", source, "rev-parse", "HEAD"]).decode().strip()
        source_files = [source / "Directory.Build.props"]
        for folder in (project.parent, source / "src/server/modules/SyntheticTaskCsv"):
            source_files += [file for file in folder.rglob("*") if file.is_file() and
                             not {"bin", "obj"}.intersection(file.relative_to(folder).parts) and
                             (file.suffix in (".cs", ".csproj") or file.name == "packages.lock.json")]
        evidence["sourceFiles"] = {str(file.relative_to(source)): digest(file.read_bytes()) for file in sorted(source_files)}
        evidence["runnerSha256"] = digest(Path(__file__).read_bytes())
        evidence["seccompSha256"] = digest((HERE / "seccomp.json").read_bytes())
        evidence["dockerfileSha256"] = digest((HERE / "Dockerfile").read_bytes())
        envelope = (fixtures / "golden-envelope.json").read_bytes()
        golden = (fixtures / "golden-one-row.csv").read_bytes()
        header = (fixtures / "golden-header.csv").read_bytes()
        empty = json.loads(envelope)
        empty.update(rows=[], selectedAttestations=[], snapshotDigest="")
        def canonical(value):
            return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).replace("&", "\\u0026").encode()
        empty["snapshotDigest"] = digest(canonical(empty))
        empty_input = canonical(empty)
        evidence["fixtureSha256"] = digest(envelope)
        evidence["goldenSha256"] = digest(golden)
        with tempfile.TemporaryDirectory(prefix="phase1b-csv-linux-") as directory:
            context = Path(directory)
            evidence["rendererClosure"] = sealed_publish(dotnet, project, context / "renderer")
            evidence["probeClosure"] = sealed_publish(dotnet, HERE / "probe/Probe.csproj", context / "probe",
                                                        ["-p:Phase1BCsvProject=" + str(source / "src/server/modules/SyntheticTaskCsv/SyntheticTaskCsv.csproj")])
            for name in ("renderer", "probe"):
                for payload, expected in ((envelope, golden), (empty_input, header)):
                    actual = command([dotnet, context / name / "SyntheticCsvRenderer.dll", *(["benign"] if name == "probe" else [])], input=payload)
                    if actual != expected:
                        raise RuntimeError("Native exact-byte parity failed")
            evidence["nativeParity"] = "PASS"
            evidence["nativeParityAssertions"] = 4
            if args.check_only:
                evidence["reason"] = "Explicit native-only invocation; Linux isolation not executed"
                code = 0
                return code
            if shutil.which("docker") is None:
                evidence["reason"] = "Docker CLI unavailable; Linux isolation not executed"
                code = 77
                return code
            server = json.loads(command(["docker", "version", "--format", "{{json .Server}} "]))
            if server["Os"] != "linux":
                raise RuntimeError("Linux Docker server required")
            evidence["dockerVersion"] = server["Version"]
            base = os.environ.get("Phase1BRuntimeImage", "mcr.microsoft.com/dotnet/runtime:10.0.12")
            base_meta = json.loads(command(["docker", "image", "inspect", base]))[0]
            if base_meta["Os"] != "linux" or base_meta["Architecture"] not in ("arm64", "amd64"):
                raise RuntimeError("Preloaded Linux runtime image required")
            evidence["runtimeImageId"] = base_meta["Id"]
            evidence["runtimeImageRepoDigests"] = base_meta.get("RepoDigests", [])
            shutil.copyfile(HERE / "Dockerfile", context / "Dockerfile")
            local_base = "phase1b-csv-base:" + uuid.uuid4().hex
            command(["docker", "tag", base_meta["Id"], local_base])
            images.append(local_base)
            build_env = dict(os.environ, DOCKER_BUILDKIT="0")
            for target in ("renderer", "probe"):
                tag = "phase1b-csv-" + target + ":" + uuid.uuid4().hex
                command(["docker", "build", "--pull=false", "--network=none", "--build-arg", "BASE_IMAGE=" + local_base,
                         "--target", target, "--tag", tag, context], env=build_env)
                images.append(tag)
            renderer, probe = images[-2:]
            cases = [("renderer-parity", renderer, None, envelope), ("renderer-header-parity", renderer, None, empty_input), ("renderer-invalid", renderer, None, b"{}"),
                     ("renderer-input-limit", renderer, None, b" " * (LIMITS["input"] + 1))]
            cases += [(mode, probe, mode, envelope) for mode in ("identity", "environment", "network", "write-app", "write-temp",
                      "untrusted-read", "fork", "wall", "stdout", "stderr", "cpu", "managed-memory", "native-memory", "pids")]
            for name, image, mode, payload in cases:
                result, output = execute_container(image, payload, mode)
                result["case"] = name
                if name in ("wall", "stdout", "stderr"):
                    passed = name in result["violations"]
                elif name == "native-memory":
                    passed = result["oomKilled"] and result["exitCode"] != 0 and not result["violations"]
                elif name in ("cpu", "managed-memory", "pids"):
                    passed = result["exitCode"] != 0 and not result["violations"] and not output and not result["oomKilled"]
                    if name == "cpu":
                        passed = passed and result["exitCode"] in (137, 152)
                    else:
                        passed = passed and result["exitCode"] == 2
                elif name in ("renderer-invalid", "renderer-input-limit"):
                    passed = result["exitCode"] == 2 and not output and not result["violations"]
                else:
                    passed = result["exitCode"] == 0 and output == (header if name == "renderer-header-parity" else golden) and not result["violations"] and result["stderrBytes"] == 0
                result["status"] = "PASS" if passed else "FAIL"
                evidence["cases"].append(result)
                if not passed:
                    raise RuntimeError("Linux boundary case failed: " + name)
            evidence["linuxStatus"] = "PASS"
            code = 0
            return code
    except (OSError, RuntimeError, ValueError, subprocess.SubprocessError, KeyError) as error:
        evidence["linuxStatus"] = "FAIL" if evidence["cases"] else "NOT_EXECUTED"
        # Do not persist subprocess stderr or exception values: they can contain host details.
        evidence["failureType"] = type(error).__name__
        return code
    finally:
        for image in reversed(images):
            subprocess.run(["docker", "image", "rm", image], timeout=30,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
        args.evidence.parent.mkdir(parents=True, exist_ok=True)
        args.evidence.write_text(json.dumps(evidence, indent=2, sort_keys=True) + "\n")
        print(json.dumps({"nativeParity": evidence["nativeParity"], "linuxStatus": evidence["linuxStatus"],
                          "caseCount": len(evidence["cases"]), "evidence": str(args.evidence)}, sort_keys=True))


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Independent actual-process checks for the inert Azure development package.

Usage: python3 verify.py --dotnet PINNED_DOTNET --assembly PUBLISHED_HOST_DLL
Runs only local diagnostics. No container, Azure, authentication or product test.
"""

import argparse
import http.client
import json
import os
import signal
import socket
import subprocess
import tempfile
import time
from pathlib import Path


SENTINEL = "synthetic-protected-value-do-not-log-71c4"
LIVE = {"bootstrapOnly": True}
READY = {"code": "product_not_enabled", "bootstrapOnly": True}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def port_available():
    with socket.socket() as check:
        check.bind(("127.0.0.1", 8080))


def request(method, path, body=None):
    connection = http.client.HTTPConnection("127.0.0.1", 8080, timeout=3)
    try:
        connection.request(method, path, body=body, headers={
            "Authorization": "Bearer " + SENTINEL,
            "X-Synthetic-Protected": SENTINEL,
        })
        response = connection.getresponse()
        content = response.read(1025)
        return response.status, dict(response.getheaders()), content
    finally:
        connection.close()


def no_leak(value):
    require(SENTINEL not in value, "Synthetic protected value leaked")


def stop(process):
    process.send_signal(signal.SIGTERM)
    try:
        output, errors = process.communicate(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.communicate()
        raise AssertionError("Process did not terminate gracefully within five seconds")
    no_leak(output + errors)
    require(process.returncode == 0, "SIGTERM must exit successfully")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", type=Path)
    parser.add_argument("--assembly", type=Path)
    parser.add_argument("--docker-image", help="Run real non-root/read-only container smoke checks instead")
    args = parser.parse_args()
    if args.docker_image:
        require(args.dotnet is None and args.assembly is None, "Choose process or container mode")
        from container_smoke import verify_container
        verify_container(args.docker_image)
        return
    require(args.dotnet is not None and args.assembly is not None, "Pinned runtime and compiled assembly required")
    require(args.dotnet.is_file() and args.assembly.is_file(), "Pinned runtime and compiled host required")
    command = [str(args.dotnet.resolve()), str(args.assembly.resolve())]
    environment = dict(os.environ)
    environment.update({"SYNTHETIC_PROTECTED_CONFIGURATION": SENTINEL,
                        "ASPNETCORE_URLS": "http://127.0.0.1:8099",
                        "ASPNETCORE_HTTP_PORTS": "8099",
                        "ASPNETCORE_LOGGING__CONSOLE__LOGLEVEL__DEFAULT": "Trace"})
    port_available()
    refused = [[], ["--role", "web"], ["--azure-development-bootstrap"],
               ["--azure-development-bootstrap", "--role", "unknown"],
               ["--role", "web", "--azure-development-bootstrap"],
               ["--azure-development-bootstrap", "--role", "web", "--unknown", SENTINEL],
               ["--azure-development-bootstrap", "--role", "web", "--role", "worker"],
               ["--azure-development-bootstrap", "--role", "WEB"]]
    with tempfile.TemporaryDirectory(prefix="iga-bootstrap-test-") as working_directory:
        # Configuration must not be read from an arbitrary working directory.
        Path(working_directory, "appsettings.json").write_text(json.dumps({
            "SyntheticProtected": SENTINEL, "Urls": "http://127.0.0.1:8099",
            "Logging": {"LogLevel": {"Default": "Trace"}}}))
        for arguments in refused:
            result = subprocess.run(command + arguments, cwd=working_directory, env=environment,
                                    capture_output=True, text=True, timeout=5)
            require(result.returncode != 0, "Absent, malformed or unknown startup mode must fail")
            require(len(result.stdout + result.stderr) <= 512, "Startup refusal must be small and fixed")
            no_leak(result.stdout + result.stderr)
        print(f"PASS: {len(refused)} actual-process startup refusals")
        web = subprocess.Popen(command + ["--azure-development-bootstrap", "--role", "web"],
                               cwd=working_directory, env=environment,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline = time.monotonic() + 10
            while True:
                require(web.poll() is None, "Web process exited before becoming live")
                try:
                    status, headers, body = request("GET", "/health/live")
                    break
                except (OSError, http.client.HTTPException):
                    require(time.monotonic() < deadline, "Web did not bind port 8080 in ten seconds")
                    time.sleep(0.05)
            for path, expected_status, expected_body in (("/health/live", 200, LIVE),
                                                          ("/health/ready", 503, READY)):
                status, headers, body = request("GET", path)
                require(status == expected_status and json.loads(body) == expected_body,
                        "Probe must expose only static bootstrap diagnostic state")
                require(headers.get("Content-Type", "").startswith("application/json")
                        and len(body) <= 128, "Probe must be small JSON")
                no_leak(body.decode() + json.dumps(headers))
                require("Set-Cookie" not in headers and "Location" not in headers,
                        "Bootstrap must not create sessions or redirect")
            methods = ("HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "TRACE", "CONNECT")
            paths = ("/", "/login", "/signin-oidc", "/api", "/api/evidence", "/health",
                     "/health/live/", "/health/ready/", "/HEALTH/LIVE", "/health/%6cive",
                     "/health/live/../ready", "/health/live?payload=" + SENTINEL,
                     "/health/ready?payload=" + SENTINEL, "/unknown?payload=" + SENTINEL)
            count = 0
            for method, path in [(method, path) for method in methods
                                 for path in ("/health/live", "/health/ready")] + [
                                     ("GET", path) for path in paths] + [("POST", "/api/evidence")]:
                status, headers, body = request(method, path, SENTINEL if method == "POST" else None)
                require(status in (401, 404),
                        f"Non-probe {method} {path.split('?')[0]} returned {status}, expected refusal")
                require(len(body) <= 256 and "Set-Cookie" not in headers, "Refusal must not expose product content")
                no_leak(body.decode(errors="replace") + json.dumps(headers))
                count += 1
            print(f"PASS: two static probes; {count} actual HTTP method/path refusals; no protected response values")
            stop(web)
            print("PASS: web graceful SIGTERM; configuration/request sentinels absent from captured logs")
        finally:
            if web.poll() is None:
                web.kill()
                web.communicate()
        port_available()
        worker = subprocess.Popen(command + ["--azure-development-bootstrap", "--role", "worker"],
                                  cwd=working_directory, env=environment,
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            time.sleep(0.3)
            require(worker.poll() is None, "Inert worker must remain alive until termination")
            with socket.socket() as check:
                check.settimeout(1)
                require(check.connect_ex(("127.0.0.1", 8080)) != 0, "Worker must not expose HTTP")
            stop(worker)
            print("PASS: worker has no HTTP listener and exits gracefully on SIGTERM without protected logs")
        finally:
            if worker.poll() is None:
                worker.kill()
                worker.communicate()
    print("NOT VERIFIED: container build/run/scan/SBOM, Azure image pull, sign-in, queue/data access and full G1.")


if __name__ == "__main__":
    main()

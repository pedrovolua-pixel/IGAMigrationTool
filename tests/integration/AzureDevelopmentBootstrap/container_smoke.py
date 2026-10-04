"""Real Docker smoke checks; invoked by verify.py --docker-image IMAGE.

Requires a local Docker engine. No Azure or registry credentials are requested.
"""

import http.client
import json
import shutil
import subprocess
import time
import uuid


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def docker(*arguments):
    result = subprocess.run(["docker", *arguments], capture_output=True, text=True, timeout=30)
    require(result.returncode == 0, "Docker operation failed: " + arguments[0] + ": " + result.stderr[:512])
    return result.stdout.strip()


def verify_container(image):
    require(shutil.which("docker") is not None, "Container verification requires an installed Docker engine")
    docker("info", "--format", "{{.ServerVersion}}")
    user = docker("image", "inspect", image, "--format", "{{.Config.User}}")
    require(user and user.split(":")[0] not in ("0", "root"), "Image must select a non-root runtime user")
    for role in ("web", "worker"):
        name = "iga-bootstrap-verify-" + uuid.uuid4().hex[:12]
        started = False
        try:
            options = ["run", "--detach", "--name", name, "--read-only", "--cap-drop=ALL",
                       "--security-opt=no-new-privileges:true", "--memory=256m", "--cpus=0.5",
                       "--pids-limit=64", "--tmpfs", "/tmp:rw,noexec,nosuid,size=16m"]
            if role == "web":
                options += ["--publish", "127.0.0.1::8080"]
            else:
                options += ["--network=none"]
            docker(*options, image, "--azure-development-bootstrap", "--role", role)
            started = True
            detail = json.loads(docker("inspect", name))[0]
            require(detail["HostConfig"]["ReadonlyRootfs"] is True
                    and "ALL" in detail["HostConfig"]["CapDrop"], "Runtime filesystem/capability protections missing")
            require("no-new-privileges:true" in detail["HostConfig"]["SecurityOpt"], "Privilege escalation protection missing")
            require(detail["Config"]["User"] == user, "Container must retain the non-root image user")
            if role == "web":
                port = int(detail["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostPort"])
                require(detail["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostIp"] == "127.0.0.1",
                        "Diagnostic container publication must remain loopback-only")
                deadline = time.monotonic() + 15
                while True:
                    try:
                        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=2)
                        connection.request("GET", "/health/live")
                        response = connection.getresponse()
                        body = response.read(256)
                        connection.close()
                        require(response.status == 200 and json.loads(body) == {"bootstrapOnly": True}, "Container liveness mismatch")
                        break
                    except (OSError, http.client.HTTPException):
                        require(time.monotonic() < deadline, "Read-only container did not become live")
                        time.sleep(0.1)
                for method, path, expected in (("GET", "/health/ready", 503), ("POST", "/health/live", 404),
                                                ("GET", "/signin-oidc", 404), ("GET", "/health/%6cive", 404)):
                    connection = http.client.HTTPConnection("127.0.0.1", port, timeout=3)
                    connection.request(method, path)
                    response = connection.getresponse()
                    body = response.read(256)
                    connection.close()
                    require(response.status == expected, "Container readiness/refusal mismatch")
                    if expected == 503:
                        require(json.loads(body) == {"code": "product_not_enabled", "bootstrapOnly": True}, "Container must remain product-not-ready")
            else:
                require(detail["HostConfig"]["NetworkMode"] == "none" and not detail["HostConfig"]["PortBindings"], "Worker cannot expose ports or network")
                time.sleep(0.2)
                require(json.loads(docker("inspect", name))[0]["State"]["Running"], "Inert worker exited early")
            docker("stop", "--time", "5", name)
            detail = json.loads(docker("inspect", name))[0]
            require(detail["State"]["ExitCode"] == 0 and not detail["State"]["OOMKilled"], "Container must terminate gracefully without resource failure")
            print(f"PASS: actual {role} container non-root/read-only/drop-capability smoke and graceful SIGTERM")
        finally:
            if started:
                docker("rm", "--force", name)
    print("NOT VERIFIED: image vulnerability scan/SBOM, Azure private pull, identity/sign-in/data access or full G1.")

"""Actual Linux non-root/read-only container smoke; no registry push or Azure access."""
import json
import re
import shutil
import subprocess
import uuid
from verify import IDENTITIES, probe, require, wait_live


def docker(*arguments):
    result = subprocess.run(['docker', *arguments], capture_output=True, text=True, timeout=30)
    require(result.returncode == 0, 'Docker operation failed: ' + arguments[0])
    return result.stdout.strip()


def verify_container(image):
    require(shutil.which('docker') is not None, 'Actual Docker engine required')
    require(re.fullmatch(r'iga-bff-development:[a-z0-9][a-z0-9_.-]{0,80}', image), 'Local diagnostic image only')
    docker('info', '--format', '{{.ServerVersion}}')
    user = docker('image', 'inspect', image, '--format', '{{.Config.User}}')
    require(user == '1654', 'Reviewed non-root runtime user required')
    name = 'iga-bff-disabled-verify-' + uuid.uuid4().hex[:12]
    started = False
    try:
        arguments = ['run', '--detach', '--name', name, '--read-only', '--cap-drop=ALL',
                     '--security-opt=no-new-privileges:true', '--memory=256m', '--cpus=0.5',
                     '--pids-limit=64', '--tmpfs', '/tmp:rw,noexec,nosuid,size=16m',
                     '--publish', '127.0.0.1::8080']
        for key, value in IDENTITIES.items():
            arguments += ['--env', key + '=' + value]
        arguments += ['--env', 'IGA_BFF_CONTROL_PLANE_CONNECTION=Host=127.0.0.1;Port=1;Database=iga_synthetic_bff_host;Username=iga_synthetic']
        docker(*arguments, image)
        started = True
        detail = json.loads(docker('inspect', name))[0]
        require(detail['HostConfig']['ReadonlyRootfs'] and 'ALL' in detail['HostConfig']['CapDrop'], 'Filesystem/capability restrictions')
        require('no-new-privileges:true' in detail['HostConfig']['SecurityOpt'], 'No privilege escalation')
        require(detail['Config']['User'] == user, 'Runtime user unchanged')
        binding = detail['NetworkSettings']['Ports']['8080/tcp'][0]
        require(binding['HostIp'] == '127.0.0.1', 'Loopback-only local diagnostic publication')
        port = int(binding['HostPort'])
        wait_live(port)
        probe(port)
        require(docker('logs', name) == '', 'No diagnostic configuration/request logs')
        docker('stop', '--time', '5', name)
        state = json.loads(docker('inspect', name))[0]['State']
        require(state['ExitCode'] == 0 and not state['OOMKilled'], 'Graceful non-root container shutdown')
        print('PASS actual Linux BFF container: non-root/read-only, exact probes/refused application paths/callbacks, no cookies/log output, graceful SIGTERM')
    finally:
        if started:
            docker('rm', '--force', name)

#!/usr/bin/env python3
"""Actual process/container checks for the hard-disabled diagnostic BFF only."""
import argparse
import http.client
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import tempfile
import threading
import time

SENTINEL = 'synthetic-do-not-log-bff-host-c3b3'
IDENTITIES = {
    'IGA_BFF_TENANT_ID': '11111111-1111-4111-8111-111111111111',
    'IGA_BFF_CLIENT_ID': '22222222-2222-4222-8222-222222222222',
    'IGA_BFF_MANAGED_IDENTITY_CLIENT_ID': '33333333-3333-4333-8333-333333333333',
}
COUNT = 0


def require(value, message):
    global COUNT
    COUNT += 1
    if not value:
        raise AssertionError(message)


def no_leak(text):
    require(SENTINEL not in text, 'Protected sentinel escaped')


class ConnectionTrap:
    def __init__(self):
        self.listener = socket.socket()
        self.listener.bind(('127.0.0.1', 0))
        self.listener.listen()
        self.listener.settimeout(0.1)
        self.port = self.listener.getsockname()[1]
        self.count = 0
        self.closed = False
        self.thread = threading.Thread(target=self.run, daemon=True)
        self.thread.start()

    def run(self):
        while not self.closed:
            try:
                connection, _ = self.listener.accept()
                self.count += 1
                connection.close()
            except (OSError, socket.timeout):
                pass

    def close(self):
        self.closed = True
        self.listener.close()
        self.thread.join(timeout=1)


def request(port, method, path):
    connection = http.client.HTTPConnection('127.0.0.1', port, timeout=3)
    try:
        connection.request(method, path, body=SENTINEL if method == 'POST' else None,
                           headers={'Authorization': 'Bearer ' + SENTINEL,
                                    'Cookie': '__Secure-IgaBff=' + SENTINEL,
                                    'X-Forwarded-Proto': 'https', 'X-Forwarded-Host': 'evil.invalid',
                                    'Forwarded': 'proto=https;host=evil.invalid'})
        response = connection.getresponse()
        body = response.read(1025)
        headers = dict(response.getheaders())
        require('Set-Cookie' not in headers and 'Location' not in headers, 'No cookie or redirect permitted')
        require(headers.get('Cache-Control') == 'no-store', 'No response may be cached')
        require('Server' not in headers and len(body) < 256, 'No server disclosure or product content')
        no_leak(json.dumps(headers) + body.decode(errors='replace'))
        return response.status, body
    finally:
        connection.close()


def probe(port):
    for path, status, body in [('/health/live', 200, {'bootstrapOnly': True}),
                               ('/health/ready', 503, {'code': 'product_not_enabled', 'bootstrapOnly': True})]:
        result, content = request(port, 'GET', path)
        require(result == status and json.loads(content) == body, 'Exact bootstrap health contract')
    methods = ('HEAD', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS', 'TRACE', 'CONNECT')
    paths = ('/', '/login', '/logout', '/api', '/bff/v1/session', '/bff/v1/sign-in', '/bff/v1/sign-out',
             '/health/live/', '/health/ready/', '/HEALTH/LIVE', '/health/%6cive',
             '/health/live/../ready', '/health/live?secret=' + SENTINEL,
             '/health/ready?secret=' + SENTINEL, '//health/live', '/health//live')
    for method, path in [(m, p) for m in methods for p in ('/health/live', '/health/ready')] + [('GET', p) for p in paths]:
        status, _ = request(port, method, path)
        require(status in (404, 401), 'Unmapped application method/raw target must refuse')
    for path in ('/bff/signin-oidc', '/bff/signout-callback-oidc', '/bff/signout-oidc'):
        for method in ('GET', 'POST'):
            status, _ = request(port, method, path)
            require(status == 403, 'Disabled callback must refuse before protocol traffic, including forwarded HTTPS')


def wait_live(port, process=None):
    deadline = time.monotonic() + 15
    while True:
        if process is not None:
            require(process.poll() is None, 'Diagnostic host exited early')
        try:
            status, _ = request(port, 'GET', '/health/live')
            require(status == 200, 'Expected live diagnostic')
            return
        except (OSError, http.client.HTTPException):
            require(time.monotonic() < deadline, 'Host did not bind')
            time.sleep(0.05)


def process_mode(dotnet, assembly):
    command = [str(dotnet.resolve()), str(assembly.resolve())]
    sql, proxy = ConnectionTrap(), ConnectionTrap()
    try:
        environment = {k: v for k, v in os.environ.items() if 'LIVESIGNINENABLED' not in k.replace('_', '').upper()}
        environment.update(IDENTITIES)
        environment.update({'IGA_BFF_CONTROL_PLANE_CONNECTION': f'Host=127.0.0.1;Port={sql.port};Database=iga_synthetic_bff_host;Username=iga_synthetic',
                            'ASPNETCORE_URLS': 'http://127.0.0.1:8099', 'ASPNETCORE_HTTP_PORTS': '8099',
                            'SYNTHETIC_PROTECTED_CONFIGURATION': SENTINEL,
                            'HTTPS_PROXY': f'http://127.0.0.1:{proxy.port}', 'HTTP_PROXY': f'http://127.0.0.1:{proxy.port}'})
        with tempfile.TemporaryDirectory(prefix='iga-disabled-bff-') as directory:
            settings = Path(directory, 'appsettings.json')
            settings.write_text(json.dumps({'Bff': {'LiveSignInEnabled': True}, 'Synthetic': SENTINEL, 'Urls': 'http://127.0.0.1:8099'}))
            cases = [([], {}), (['--bff-development-disabled', '--live'], {}),
                     (['--bff-development-disabled', '--bff-development-disabled'], {}),
                     (['--bff-development-disabled'], {'IGA_BFF_TENANT_ID': ''}),
                     (['--bff-development-disabled'], {'IGA_BFF_CLIENT_ID': SENTINEL}),
                     (['--bff-development-disabled'], {'IGA_BFF_MANAGED_IDENTITY_CLIENT_ID': IDENTITIES['IGA_BFF_CLIENT_ID']}),
                     (['--bff-development-disabled'], {'IGA_BFF_CONTROL_PLANE_CONNECTION': ''}),
                     (['--bff-development-disabled'], {'IGA_BFF_CONTROL_PLANE_CONNECTION': SENTINEL}),
                     (['--bff-development-disabled'], {'Bff__LiveSignInEnabled': 'true'}),
                     (['--bff-development-disabled'], {'IGA_BFF_LIVE_SIGN_IN_ENABLED': 'false'}),
                     (['--bff-development-disabled'], {'IGA_BFF_ACTIVATION': 'true'}),
                     (['--bff-development-disabled'], {'ASPNETCORE_FORWARDEDHEADERS_ENABLED': 'true'})]
            for arguments, changes in cases:
                result = subprocess.run(command + arguments, cwd=directory, env=environment | changes,
                                        capture_output=True, text=True, timeout=10)
                require(result.returncode == 2, 'Invalid/activation configuration must refuse startup')
                require(len(result.stdout + result.stderr) <= 256, 'Refusal must be fixed and small')
                no_leak(result.stdout + result.stderr)
            with socket.socket() as available:
                available.bind(('127.0.0.1', 8080))
            host = subprocess.Popen(command + ['--bff-development-disabled'], cwd=directory, env=environment,
                                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            try:
                wait_live(8080, host)
                probe(8080)
                with socket.socket() as ignored_port:
                    require(ignored_port.connect_ex(('127.0.0.1', 8099)) != 0, 'Environment/file listener override must be ignored')
                require(sorted(p.name for p in Path(directory).iterdir()) == ['appsettings.json'], 'No key/session/file persistence')
                host.send_signal(signal.SIGTERM)
                output, errors = host.communicate(timeout=5)
                require(host.returncode == 0, 'Graceful SIGTERM must succeed')
                no_leak(output + errors)
                require(output == '' and errors == '', 'No configuration/request/provider log output')
            finally:
                if host.poll() is None:
                    host.kill()
                    host.communicate()
        require(sql.count == 0, 'Host must never connect to PostgreSQL')
        require(proxy.count == 0, 'Host must never contact configured provider/network proxy')
        print(f'PASS {COUNT} actual-process assertions: startup/config refusal, exact health/raw paths, disabled callbacks, no cookies, SQL/proxy traffic or persistence, graceful shutdown')
    finally:
        sql.close()
        proxy.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', type=Path)
    parser.add_argument('--assembly', type=Path)
    parser.add_argument('--docker-image')
    args = parser.parse_args()
    if args.docker_image:
        require(args.dotnet is None and args.assembly is None, 'Choose one execution mode')
        from container_smoke import verify_container
        verify_container(args.docker_image)
    else:
        require(args.dotnet is not None and args.assembly is not None and args.dotnet.is_file() and args.assembly.is_file(), 'Pinned runtime and built assembly required')
        process_mode(args.dotnet, args.assembly)
    print('NOT VERIFIED: production BFF/provider/CA, protected shared key ring, Azure deployment, image acceptance or G1/G3.')


if __name__ == '__main__':
    main()

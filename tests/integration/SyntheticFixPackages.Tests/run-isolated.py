"""Launch .NET fixture tooling with an explicit credential-free environment.

Preserve HOME's actual value; do not load application configuration or print values.
Usage: python3 run-isolated.py /absolute/dotnet <dotnet arguments...>
"""
import os
from pathlib import Path
import subprocess
import sys

if len(sys.argv) < 3 or not Path(sys.argv[1]).is_absolute():
    raise SystemExit('FAIL isolated launch arguments')
allowed = ['PATH', 'HOME', 'TMPDIR', 'TEMP', 'TMP', 'TZ', 'SystemRoot', 'WINDIR']
environment = {key: os.environ[key] for key in allowed if key in os.environ}
environment['DOTNET_ROOT'] = str(Path(sys.argv[1]).resolve().parent)
environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
environment['DOTNET_NOLOGO'] = '1'
# Cache is a tool dependency path, not a connection or application setting.
if 'NUGET_PACKAGES' in os.environ:
    environment['NUGET_PACKAGES'] = os.environ['NUGET_PACKAGES']
code = subprocess.call(sys.argv[1:], env=environment)
raise SystemExit(code)

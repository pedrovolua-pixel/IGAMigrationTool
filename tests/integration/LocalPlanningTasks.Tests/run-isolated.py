"""Run only the supplied .NET command in a closed test environment; preserve no application/provider secrets."""
from pathlib import Path
import os
import subprocess
import sys

ALLOW = {"PATH", "HOME", "TMPDIR", "TEMP", "TMP", "SystemRoot", "WINDIR", "COMSPEC", "PATHEXT", "USERPROFILE", "LOCALAPPDATA", "APPDATA"}
if len(sys.argv) < 3 or not Path(sys.argv[1]).is_absolute():
    raise SystemExit("FAIL isolated_launcher_arguments")
executable = Path(sys.argv[1]).resolve(strict=True)
environment = {key: value for key, value in os.environ.items() if key in ALLOW}
environment["DOTNET_ROOT"] = str(executable.parent)
environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
environment["DOTNET_NOLOGO"] = "1"
environment["DOTNET_hostBuilder__reloadConfigOnChange"] = "false"
environment["DOTNET_CLI_HOME"] = "/private/tmp/iga-cycle14-v14-cli" if os.name != "nt" else environment.get("USERPROFILE", str(Path.home()))
if "IGA_PLANNING_TASK_TEST_DATABASE" in os.environ:
    environment["IGA_PLANNING_TASK_TEST_DATABASE"] = os.environ["IGA_PLANNING_TASK_TEST_DATABASE"]
result = subprocess.run([str(executable), *sys.argv[2:]], env=environment, check=False)
raise SystemExit(result.returncode)

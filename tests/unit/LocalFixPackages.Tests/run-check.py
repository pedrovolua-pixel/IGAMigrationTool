import os, pathlib, subprocess, sys
root = pathlib.Path(__file__).resolve().parents[3]
sdk = '/private/tmp/iga-dotnet-10.0.401'
env = {k: os.environ[k] for k in ['PATH', 'HOME', 'TMPDIR', 'LANG', 'LC_ALL', 'SHELL', 'USER', 'LOGNAME'] if k in os.environ}
env.update(DOTNET_ROOT=sdk, DOTNET_CLI_HOME='/private/tmp/iga-coordinator-cli', DOTNET_NOLOGO='1')
log = root / 'tests/unit/LocalFixPackages.Tests/execution' / sys.argv[1]
with log.open('w') as output:
    try:
        result = subprocess.run(sys.argv[2:], cwd=root, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=180)
    except subprocess.TimeoutExpired:
        output.write('\nBOUND: assigned check exceeded 180 seconds; owned child terminated.\n')
        sys.exit(124)
sys.exit(result.returncode)

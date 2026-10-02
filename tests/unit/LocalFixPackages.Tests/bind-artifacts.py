import hashlib, json, pathlib, subprocess, sys, xml.etree.ElementTree as ET
root = pathlib.Path(__file__).resolve().parents[3]
packet = root / 'tests/unit/LocalFixPackages.Tests'
manifest = packet / 'execution.json'
base = 'fe0a09e24a61dfcb7885c0989942a32ba0e22b71'

def binding(path):
    data = path.read_bytes()
    return dict(path=path.relative_to(root).as_posix(), bytes=len(data), sha256=hashlib.sha256(data).hexdigest())

if '--write' in sys.argv:
    paths = set()
    projects = set()
    def project(path):
        path = path.resolve()
        if path in projects: return
        projects.add(path)
        paths.add(path)
        for name in ['packages.lock.json', 'obj/project.assets.json', 'obj/' + path.name + '.nuget.g.props', 'obj/' + path.name + '.nuget.g.targets']:
            candidate = path.parent / name
            if candidate.exists(): paths.add(candidate)
        for candidate in path.parent.rglob('*.cs'):
            relative = candidate.relative_to(path.parent)
            if 'bin' not in relative.parts and 'obj' not in relative.parts: paths.add(candidate)
        for reference in ET.parse(path).getroot().iter('ProjectReference'):
            project(path.parent / reference.attrib['Include'])
        for resource in ET.parse(path).getroot().iter('EmbeddedResource'):
            paths.update(path.parent.glob(resource.attrib['Include']))
    project(packet / 'LocalFixPackages.Tests.csproj')
    for name in ['global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config', '.editorconfig']:
        path = root / name
        if path.exists(): paths.add(path)
    paths.update(path for path in packet.glob('*') if path.is_file() and path != manifest)
    paths.update(path for path in (packet / 'execution').glob('*') if path.is_file())
    paths.update(path for path in (packet / 'bin/Release/net10.0').rglob('*') if path.is_file())
    copies = []
    for copy, original in [
        ('historical-guidance-golden.json','tests/unit/RecommendationGuidance.Tests/golden-payload.json'),
        ('historical-standard-golden.json','tests/unit/LocalAiPreview.Tests/golden-historical-standard.json'),
        ('historical-comparison-golden.json','tests/unit/LocalAiPreview.Tests/golden-historical-comparison.json')]:
        source = subprocess.check_output(['git','show',base + ':' + original], cwd=root)
        assert (packet / copy).read_bytes() == source
        copies.append(dict(copy=(packet / copy).relative_to(root).as_posix(), original=original,
            sourceCommit=base, sha256=hashlib.sha256(source).hexdigest(), bytes=len(source)))
    checks = [
        ('restore.log', ['restore','tests/unit/LocalFixPackages.Tests/LocalFixPackages.Tests.csproj','--locked-mode','--disable-parallel','-p:NuGetAudit=true','-p:NuGetAuditMode=all']),
        ('audit.log', ['list','tests/unit/LocalFixPackages.Tests/LocalFixPackages.Tests.csproj','package','--vulnerable','--include-transitive','--no-restore']),
        ('build.log', ['build','tests/unit/LocalFixPackages.Tests/LocalFixPackages.Tests.csproj','--no-restore','--configuration','Release','--disable-build-servers','-p:UseSharedCompilation=false','-warnaserror','-m:1']),
        ('unit.log', ['tests/unit/LocalFixPackages.Tests/bin/Release/net10.0/LocalFixPackages.UnitTests.dll']),
        ('sdk.log', ['--info'])]
    include = ['src/server/modules/AssessmentRuns/' + name + '.cs' for name in ['DemoFixPackageCatalog','DemoAnalysisCatalog','DemoFixtureCatalog','SyntheticRunContracts','SyntheticDurableRunEngine']]
    include += ['src/server/modules/RecommendationGuidance/RecommendationGuidanceBuilder.cs','src/server/modules/ReportDrafts/DraftSnapshotBuilder.cs','src/server/hosts/LocalConsultantDemo/DemoFixPackageProjection.cs','src/server/hosts/LocalConsultantDemo/DemoRecommendationGuidanceProjection.cs']
    include += ['tests/unit/LocalFixPackages.Tests/' + name + '.cs' for name in ['Program','SavedFixture','IndependentCanonical','GuidanceFixture','DraftFixture']]
    checks.append(('format.log',['format','tests/unit/LocalFixPackages.Tests/LocalFixPackages.Tests.csproj','--no-restore','--verify-no-changes','--include',*include,'--verbosity','minimal']))
    commands = [dict(command=['/private/tmp/iga-dotnet-10.0.401/dotnet',*command], exitCode=0,
        launcher='tests/unit/LocalFixPackages.Tests/run-check.py',transcript='tests/unit/LocalFixPackages.Tests/execution/' + log) for log,command in checks]
    commands += [dict(command=['/usr/bin/python3','tests/unit/LocalFixPackages.Tests/independent-oracle.py'],exitCode=0,launcher='tests/unit/LocalFixPackages.Tests/run-check.py',transcript='tests/unit/LocalFixPackages.Tests/execution/oracle.log')]
    commands += [dict(command=['/private/tmp/iga-parallel-tools/gitleaks',*command],exitCode=0,launcher='tests/unit/LocalFixPackages.Tests/run-check.py',transcript='tests/unit/LocalFixPackages.Tests/execution/' + log)
        for log,command in [('secrets-tests.log',['dir','tests/unit/LocalFixPackages.Tests','--redact','--no-banner','--exit-code','1']),('secrets-staged.log',['protect','--staged','--redact','--no-banner','--exit-code','1'])]]
    commands.append(dict(command=['git','diff','--cached','--check'],exitCode=0,launcher='tests/unit/LocalFixPackages.Tests/run-check.py',transcript='tests/unit/LocalFixPackages.Tests/execution/diff.log'))
    data = dict(status='PASS',baseCommit=base,branch='codex/cycle12-source',workingDirectory=str(root),
        sourceAuthority='Immutable worker commit enclosing this record; final compiled source hashes and original outputs are bound below. Compilation occurred with the declared base Git HEAD.',
        assertions=553,environment=dict(inheritance='Only PATH HOME TMPDIR LANG LC_ALL SHELL USER LOGNAME; no database/provider/application variables.',
        dotnetRoot='/private/tmp/iga-dotnet-10.0.401',cliHome='/private/tmp/iga-coordinator-cli',sdk='10.0.401',childTimeoutSeconds=180),
        checks=commands,artifacts=[binding(path) for path in sorted(paths)],historicalCopyEqualities=copies,
        failureHistory=[
            dict(transcript='tests/unit/LocalFixPackages.Tests/execution/restore-initial-stalled.log',status='STOPPED',detail='Initial sandbox/cache-less restore did not progress past Determining projects to restore. Identified exact owned PID 51237 and sent TERM. Subsequent bounded allowlisted audited restore passed; no audit claim for initial attempt.'),
            dict(transcript='tests/unit/LocalFixPackages.Tests/execution/unit-initial.log',status='FAILED',detail='Historical test scaffold omitted planner DeclaredItems, so summary had no completion. Corrected only test input. Initial composite shell returned zero because it then printed the failure log; underlying unit exit code was not preserved, and no pass is claimed.'),
            dict(transcript='tests/unit/LocalFixPackages.Tests/execution/format-initial.log',status='FAILED',exitCode=2,detail='Whitespace verification; correction applied only to explicitly owned files. Final full formatter check and rebuild/unit reran exact formatted source.'),
            dict(transcript='tests/unit/LocalFixPackages.Tests/execution/secrets-initial-unstaged.log',status='NOT_SCOPED',detail='Initial staging was denied by the filesystem sandbox, so the following staged scan saw no assigned patch. Staging then succeeded under scoped authorization and secrets-staged.log records the actual owned patch scan. No coverage claim for the initial empty scan.')],
        limits=['Portable pure fixture code only; no PG/HTTP/browser/provider launch or database call.','Full solution/integration/hosted/security architecture/manual/UAT/publication checks remain coordinator/V12 responsibility.','No human approval, deployed sandbox isolation, customer remediation or full milestone/gate completion is established.'])
    manifest.write_text(json.dumps(data,indent=2)+'\n')
else:
    data = json.loads(manifest.read_text())
    for item in data['artifacts']:
        assert binding(root / item['path']) == item, item['path']
    for item in data['historicalCopyEqualities']:
        original = subprocess.check_output(['git','show',item['sourceCommit'] + ':' + item['original']], cwd=root)
        assert hashlib.sha256(original).hexdigest() == item['sha256'] and len(original) == item['bytes']
        assert (root / item['copy']).read_bytes() == original
    print('PASS',len(data['artifacts']),'exact source/output/transcript bindings and',len(data['historicalCopyEqualities']),'unchanged historical golden equalities')

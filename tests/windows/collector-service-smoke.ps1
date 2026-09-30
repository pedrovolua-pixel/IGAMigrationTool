param(
    [Parameter(Mandatory = $true)]
    [string] $ExecutablePath
)

$ErrorActionPreference = 'Stop'
$serviceName = 'IgaPilotCollectorSmoke'
$directory = Join-Path $env:ProgramData $serviceName
$configPath = Join-Path $directory 'collector.json'
$outputPath = Join-Path $directory 'baseline.igapkg'

try {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    & icacls $directory /inheritance:r | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to protect test directory inheritance.' }
    & icacls $directory /grant:r '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to protect test directory permissions.' }

    $config = [ordered]@{
        schemaVersion = 1
        scopeId = '11111111-1111-1111-1111-111111111111'
        exactBuild = '10.0.0.1'
        queryPackId = '22222222-2222-2222-2222-222222222222'
        queryPackVersion = 1
        queryPackSha256 = 'a' * 64
        fieldPolicyId = '33333333-3333-3333-3333-333333333333'
        fieldPolicyVersion = 1
        fieldPolicySha256 = 'b' * 64
        sqlDescriptorRef = (Join-Path $directory 'sql.ref')
        timeZoneId = 'Eastern Standard Time'
        localRunTime = '02:30'
        enabled = $false
        maxPageSize = 100
        maxRows = 1000
        maxDurationSeconds = 60
        maxLocalBytes = 1000000
        retentionHours = 24
        offlineRecipientKeyId = '44444444-4444-4444-4444-444444444444'
    }
    $config | ConvertTo-Json -Compress | Set-Content -Path $configPath -Encoding utf8

    $binaryPath = '"' + $ExecutablePath + '" service --config "' + $configPath + '"'
    New-Service -Name $serviceName -BinaryPathName $binaryPath -StartupType Manual | Out-Null
    Start-Service -Name $serviceName
    $running = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if ((Get-Service -Name $serviceName).Status -eq 'Running') {
            $running = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $running) { throw 'Collector service did not reach Running.' }

    $status = & $ExecutablePath status --config $configPath
    if ($LASTEXITCODE -ne 0 -or $status -ne 'COLLECTOR_DISABLED') {
        throw 'Collector status did not report the disabled state.'
    }
    $oneShot = & $ExecutablePath collect-once --config $configPath --offline-output $outputPath 2>&1
    if ($LASTEXITCODE -ne 5 -or $oneShot -ne 'COLLECTOR_DISABLED' -or (Test-Path $outputPath)) {
        throw 'Blocked one-shot collection had an unexpected result.'
    }
    Write-Output 'Windows service start/stop, status, and blocked one-shot smoke checks passed.'
}
finally {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($null -ne $service) {
        if ($service.Status -ne 'Stopped') {
            Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        }
        & sc.exe delete $serviceName | Out-Null
    }
    if (Test-Path $directory) {
        Remove-Item -Path $directory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

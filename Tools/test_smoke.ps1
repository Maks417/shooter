[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Every player, editor and build below is synthetic. This tests wrapper decisions only.
$root = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $root ('Artifacts/tooling-tests/smoke/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$null = New-Item -ItemType Directory -Path $evidence
$common = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:fixture = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'fixture.json') -Raw | ConvertFrom-Json
function Get-ShooterUnity { [pscustomobject]@{ LockHash = 'synthetic-lock' } }
function New-ShooterRun($Action, $Unity) {
    $path = Join-Path $PSScriptRoot 'run'
    $null = New-Item -ItemType Directory -Path $path
    [pscustomobject]@{ Path = $path; Report = [ordered]@{ result = 'FAIL'; error = $null } }
}
function Assert-ShooterPackedProvenance($Manifest) {}
function Get-ShooterHash($Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Invoke-ShooterProcess {
    param($Executable, $ProcessArguments, $WorkingDirectory, $LogPath, $TimeoutSeconds)
    $expected = if ($script:fixture.target -ceq 'Server') { '-nographics' } else { '-force-d3d11' }
    if ($ProcessArguments -cnotcontains $expected) { throw 'Expected client/server process argument missing.' }
    $logIndex = [Array]::IndexOf($ProcessArguments, '-logFile') + 1
    if ($script:fixture.writeLog) { [IO.File]::WriteAllText($ProcessArguments[$logIndex], $script:fixture.log) }
    if ($script:fixture.processFailure) { throw 'Synthetic nonzero process exit.' }
}
function Save-ShooterRun($Run) { $Run.Report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Run.Path 'run.json') }
'@
$cases = @(
    @{ name='client-minimum'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=60`n"; pass=$true },
    @{ name='server-120'; target='Server'; log="SHOOTER_READY_SERVER`r`nSHOOTER_SMOKE_PASS mode=Server ticks=120`r`n"; pass=$true },
    @{ name='unity-stack-lines'; target='Client'; log="Unity startup`nSHOOTER_READY_CLIENT`nUnityEngine.Debug:Log (object)`nSHOOTER_SMOKE_PASS mode=Client ticks=120`nUnityEngine.Debug:Log (object)"; pass=$true },
    @{ name='missing-ready'; target='Client'; log='SHOOTER_SMOKE_PASS mode=Client ticks=120'; pass=$false },
    @{ name='wrong-ready'; target='Client'; log="SHOOTER_READY_SERVER`nSHOOTER_SMOKE_PASS mode=Client ticks=120"; pass=$false },
    @{ name='partial-ready'; target='Client'; log="SHOOTER_READY_CLIENT_extra`nSHOOTER_SMOKE_PASS mode=Client ticks=120"; pass=$false },
    @{ name='pass-before-ready'; target='Client'; log="SHOOTER_SMOKE_PASS mode=Client ticks=120`nSHOOTER_READY_CLIENT"; pass=$false },
    @{ name='missing-pass'; target='Client'; log='SHOOTER_READY_CLIENT'; pass=$false },
    @{ name='missing-ticks'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client"; pass=$false },
    @{ name='low-ticks'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=59"; pass=$false },
    @{ name='negative-ticks'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=-60"; pass=$false },
    @{ name='decimal-ticks'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=60.5"; pass=$false },
    @{ name='overflow-ticks'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=18446744073709551616"; pass=$false },
    @{ name='wrong-mode'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Server ticks=120"; pass=$false },
    @{ name='mode-prefix'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=ClientExtra ticks=120"; pass=$false },
    @{ name='pass-prefix'; target='Client'; log="SHOOTER_READY_CLIENT`nPREFIX SHOOTER_SMOKE_PASS mode=Client ticks=120"; pass=$false },
    @{ name='pass-suffix'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=120 trailing"; pass=$false },
    @{ name='duplicate-pass'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=120`nSHOOTER_SMOKE_PASS mode=Client ticks=120"; pass=$false },
    @{ name='failure-after-pass'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=120`nSHOOTER_FAILURE: synthetic"; pass=$false },
    @{ name='failure-before-pass'; target='Client'; log="SHOOTER_FAILURE: synthetic`nSHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=120"; pass=$false },
    @{ name='wrong-case'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=client ticks=120"; pass=$false },
    @{ name='missing-log'; target='Client'; log=''; writeLog=$false; pass=$false },
    @{ name='nonzero-process'; target='Client'; log="SHOOTER_READY_CLIENT`nSHOOTER_SMOKE_PASS mode=Client ticks=120"; processFailure=$true; pass=$false },
    @{ name='unknown-target'; target='Other'; log="SHOOTER_READY_OTHER`nSHOOTER_SMOKE_PASS mode=Other ticks=120"; pass=$false }
)
$results = @()
foreach ($case in $cases) {
    $caseRoot = Join-Path $evidence $case.name
    $null = New-Item -ItemType Directory -Path (Join-Path $caseRoot 'build/player') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Smoke.ps1') -Destination $caseRoot
    Set-Content -LiteralPath (Join-Path $caseRoot 'Unity.Common.ps1') -Value $common
    [ordered]@{ target=$case.target; log=$case.log; writeLog=($case['writeLog'] -ne $false); processFailure=($case['processFailure'] -eq $true) } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $caseRoot 'fixture.json')
    [ordered]@{ result='Succeeded'; target=$case.target; toolchainHash='synthetic-lock'; sourceAssetPaths=@(); graphicsSourceAssets=@() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $caseRoot 'build/build-manifest.json')
    $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $caseRoot 'Smoke.ps1') -BuildRunDirectory (Join-Path $caseRoot 'build') 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath (Join-Path $caseRoot 'wrapper-output.log')
    $report = Get-Content -LiteralPath (Join-Path $caseRoot 'run/run.json') -Raw | ConvertFrom-Json
    $expected = if ($case.pass) { 'PASS' } else { 'FAIL' }
    if (($exitCode -eq 0) -ne $case.pass -or $report.result -cne $expected) { throw "Case $($case.name): exit=$exitCode result=$($report.result), expected $expected; see $caseRoot" }
    if ($case.pass -and $report.completedTicks -lt 60) { throw "Case $($case.name): completed tick count was not recorded." }
    if (!$case.pass -and [string]::IsNullOrEmpty($report.error)) { throw "Case $($case.name): failure diagnostic missing." }
    $results += [ordered]@{ name=$case.name; expected=$expected; exitCode=$exitCode; result='PASS' }
}
[ordered]@{ result='PASS'; testCount=$results.Count; scope='Synthetic wrapper regression tests only; no Unity, player or hardware acceptance'; sourceSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Smoke.ps1') -Algorithm SHA256).Hash.ToLowerInvariant(); cases=$results } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'results.json')
Write-Host "PASS: $($results.Count) smoke wrapper regression cases; synthetic evidence: $evidence"
exit 0

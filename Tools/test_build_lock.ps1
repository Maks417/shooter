[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# ADR-079 regressions for the toolchain/build lock split. Synthetic package manifests only; no editor is started.
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$root = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $root ('Artifacts/tooling-tests/build-lock/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $evidence
$lock = Get-Content -LiteralPath (Join-Path $root 'toolchain-lock.json') -Raw | ConvertFrom-Json
$buildLockText = Get-Content -LiteralPath (Join-Path $root 'build-lock.json') -Raw
$manifestText = Get-Content -LiteralPath (Join-Path $root 'UnityProject/Packages/manifest.json') -Raw
$packageLockText = Get-Content -LiteralPath (Join-Path $root 'UnityProject/Packages/packages-lock.json') -Raw
$results = [Collections.Generic.List[object]]::new()

function Invoke-Case([string]$Name, [string]$ExpectedError, [scriptblock]$Mutate) {
    $case = Join-Path $evidence $Name
    $null = New-Item -ItemType Directory -Path (Join-Path $case 'UnityProject/Packages') -Force
    $manifest = $manifestText | ConvertFrom-Json
    $packageLock = $packageLockText | ConvertFrom-Json
    $buildLock = $buildLockText | ConvertFrom-Json
    & $Mutate $manifest $packageLock $buildLock
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $case 'UnityProject/Packages/manifest.json')
    $packageLock | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $case 'UnityProject/Packages/packages-lock.json')
    $actual = $null
    try { Assert-ShooterLockedPackages -Root $case -Lock $lock -BuildLock $buildLock } catch { $actual = "$_" }
    $passed = if ($ExpectedError) { $actual -and $actual.Contains($ExpectedError) } else { $null -eq $actual }
    $results.Add([ordered]@{ case = $Name; expected = $ExpectedError; actual = $actual; result = $(if ($passed) { 'PASS' } else { 'FAIL' }) }) | Out-Null
}

function Add-Dependency($Object, [string]$Name, $Value) { $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force }

Invoke-Case 'committed-packages-accepted' '' { param($m, $p, $b) }
Invoke-Case 'unlocked-package-rejected' 'Unlocked package in manifest.json: com.example.tool' {
    param($m, $p, $b)
    Add-Dependency $m.dependencies 'com.example.tool' '1.0.0'
}
Invoke-Case 'package-locked-in-build-lock-accepted' '' {
    param($m, $p, $b)
    Add-Dependency $m.dependencies 'com.example.tool' '1.0.0'
    Add-Dependency $p.dependencies 'com.example.tool' ([pscustomobject]@{ version = '1.0.0' })
    $b.packages = @($b.packages) + [pscustomobject]@{ name = 'com.example.tool'; version = '1.0.0' }
}
Invoke-Case 'build-lock-version-drift-rejected' 'Package differs from toolchain/build lock: com.unity.inputsystem' {
    param($m, $p, $b)
    $m.dependencies.'com.unity.inputsystem' = '9.9.9'
}
Invoke-Case 'engine-package-drift-rejected' 'Package differs from toolchain/build lock: com.unity.ai.navigation' {
    param($m, $p, $b)
    $m.dependencies.'com.unity.ai.navigation' = '9.9.9'
}
Invoke-Case 'engine-package-in-build-lock-rejected' 'must not repeat engine package com.unity.ai.navigation' {
    param($m, $p, $b)
    $b.packages = @($b.packages) + [pscustomobject]@{ name = 'com.unity.ai.navigation'; version = '2.0.0' }
}
Invoke-Case 'engine-module-allowed' '' {
    param($m, $p, $b)
    Add-Dependency $m.dependencies 'com.unity.modules.animation' '1.0.0'
}

# The point of the split: toolchain-lock.json no longer names runtime/test packages, so adding one cannot change toolchainHash.
$lockText = Get-Content -LiteralPath (Join-Path $root 'toolchain-lock.json') -Raw
$splitOk = $lock.schemaVersion -eq 2 -and -not $lockText.Contains('inputSystemVersion') -and -not $lockText.Contains('testFrameworkVersion') -and -not $lockText.Contains('clientBackend')
$results.Add([ordered]@{ case = 'toolchain-lock-holds-engine-fields-only'; result = $(if ($splitOk) { 'PASS' } else { 'FAIL' }) }) | Out-Null

$failed = @($results | Where-Object { $_.result -cne 'PASS' })
[ordered]@{ result = $(if ($failed.Count) { 'FAIL' } else { 'PASS' }); cases = $results } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'results.json')
if ($failed.Count) {
    $failed | ForEach-Object { Write-Host "FAIL: $($_.case) expected '$($_.expected)' got '$($_.actual)'" }
    exit 1
}
Write-Host "PASS: $($results.Count) build-lock cases; evidence: $evidence"

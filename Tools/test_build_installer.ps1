[CmdletBinding()]
param([string]$IsccPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Synthetic regression for Tools/Build-Installer.ps1. Needs the pinned Inno Setup compiler (ISCC) to build
# the passing case; no Unity, no real package, no install is performed.
$root = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $root ('Artifacts/tooling-tests/build-installer/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $evidence -Force
$results = [Collections.Generic.List[object]]::new()
$commit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'

function Write-TestFile([string]$Path, [string]$Text) {
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force
    [IO.File]::WriteAllText($Path, $Text)
}
function Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function New-Case([string]$Name) {
    $caseRoot = Join-Path $evidence $Name
    $null = New-Item -ItemType Directory -Path (Join-Path $caseRoot 'Tools/installer') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Build-Installer.ps1') -Destination (Join-Path $caseRoot 'Tools/Build-Installer.ps1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity.Common.ps1') -Destination (Join-Path $caseRoot 'Tools/Unity.Common.ps1')
    foreach ($f in @('installer-policy.json', 'shooter.iss')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "installer/$f") -Destination (Join-Path $caseRoot "Tools/installer/$f")
    }
    $pkg = Join-Path $caseRoot 'package'
    $stage = Join-Path $pkg 'Shooter-Offline'
    Write-TestFile (Join-Path $stage 'Shooter.exe') 'synthetic-exe'
    Write-TestFile (Join-Path $stage 'UnityPlayer.dll') 'synthetic-player'
    Write-TestFile (Join-Path $stage 'Shooter_Data/boot.config') 'boot'
    Write-TestFile (Join-Path $stage 'D3D12/D3D12Core.dll') 'd3d'
    Write-TestFile (Join-Path $stage 'LICENSES.txt') 'licenses'
    Write-TestFile (Join-Path $stage 'Content/map-packages/index.json') '{}'
    Write-TestFile (Join-Path $stage 'Content/map-packages/catalog.json') '{}'
    Write-TestFile (Join-Path $stage 'Content/map-packages/weapons.json') '{}'
    Write-TestFile (Join-Path $stage 'toolchain-lock.json') '{"syntheticTestOnly":true}'
    $tc = Sha (Join-Path $stage 'toolchain-lock.json')
    $ids = [ordered]@{ binaryCommit = $commit; sourceHash = 'b' * 64; toolchainHash = $tc; buildContentManifestHash = 'c' * 64; contentClosureHash = 'd' * 64; contentIndexSha256 = 'e' * 64 }
    Write-TestFile (Join-Path $stage 'identities.json') ($ids | ConvertTo-Json)
    Write-TestFile (Join-Path $stage 'rollback-pair.json') (([ordered]@{ binaryCommit = $commit; contentManifestHash = 'd' * 64 }) | ConvertTo-Json)
    $inv = Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
        [pscustomobject]@{ path = $_.FullName.Substring($stage.Length + 1).Replace('\', '/'); sha256 = (Sha $_.FullName) }
    }
    Write-TestFile (Join-Path $pkg 'inventory.json') ($inv | ConvertTo-Json)
    $val = [ordered]@{
        schemaVersion = 1; action = 'Package-Release'; sourceCommit = $commit; sourceHash = 'b' * 64; toolchainHash = $tc
        buildContentManifestHash = 'c' * 64; contentClosureHash = 'd' * 64; contentIndexSha256 = 'e' * 64
        protocolVersion = 'N1-v1'; contractVersion = 'Game-v1'; buildSourceDirty = $false; result = 'PASS'
    }
    Write-TestFile (Join-Path $pkg 'validation.json') ($val | ConvertTo-Json)
    [pscustomobject]@{ Root = $caseRoot; Package = $pkg; Stage = $stage }
}

function Edit-Json([string]$Path, [scriptblock]$Edit) {
    $o = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    & $Edit $o
    ($o | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-Case([string]$Name, [scriptblock]$Setup, [string]$ExpectedError = '', [switch]$ExpectPass, [string]$Version = '0.1.0') {
    $c = New-Case $Name
    $extraArgs = @()
    $r = & $Setup $c
    if ($r -is [hashtable] -and $r.ContainsKey('Args')) { $extraArgs = $r.Args }
    $args = @('-NoProfile', '-File', (Join-Path $c.Root 'Tools/Build-Installer.ps1'), '-PackageDirectory', $c.Package, '-ReleaseVersion', $Version,
        '-InstallerArtProject', (Join-Path $PSScriptRoot 'InstallerArt/InstallerArt.csproj')) + $extraArgs
    if ($IsccPath) { $args += @('-IsccPath', $IsccPath) }
    $log = Join-Path $c.Root 'wrapper.log'
    & (Join-Path $PSHOME 'pwsh.exe') @args *> $log
    $code = $LASTEXITCODE
    $rep = Get-ChildItem -LiteralPath (Join-Path $c.Root 'Artifacts') -Recurse -Filter installer-report.json -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($ExpectPass) {
        if ($code -ne 0 -or !$rep) { throw "$Name expected PASS; exit=$code; see $log" }
        $report = Get-Content -LiteralPath $rep.FullName -Raw | ConvertFrom-Json
        if ($report.result -cne 'PASS') { throw "$Name report not PASS" }
        if ($report.releaseAcceptance -cne 'NotClaimed') { throw "$Name must not claim acceptance" }
        if (!(Test-Path -LiteralPath $report.installerPath -PathType Leaf)) { throw "$Name missing installer exe" }
        if ((Sha $report.installerPath) -cne $report.installerSha256) { throw "$Name installer hash mismatch" }
        if (!(Test-Path -LiteralPath "$($report.installerPath).sha256")) { throw "$Name missing .sha256 file" }
    }
    else {
        if ($code -eq 0) { throw "$Name expected nonzero exit; see $log" }
        if ($rep) {
            $report = Get-Content -LiteralPath $rep.FullName -Raw | ConvertFrom-Json
            if ($report.result -cne 'FAIL') { throw "$Name report must be FAIL" }
            if ($report.installerPath) { throw "$Name must not emit an installer" }
            if ($ExpectedError -and ("$($report.failure)" -notlike "*$ExpectedError*")) { throw "$Name failure missing '$ExpectedError'; was: $($report.failure)" }
        }
        elseif ($ExpectedError -and ((Get-Content -LiteralPath $log -Raw) -notlike "*$ExpectedError*")) { throw "$Name log missing '$ExpectedError'" }
        if (Get-ChildItem -LiteralPath (Join-Path $c.Root 'Artifacts') -Recurse -Filter '*.exe' -ErrorAction SilentlyContinue) { throw "$Name emitted an exe despite failure" }
    }
    $results.Add([ordered]@{ name = $Name; result = 'PASS'; wrapperExitCode = $code; expectedError = $ExpectedError }) | Out-Null
}

try {
    Invoke-Case 'clean-pass' -ExpectPass { param($c) }
    Invoke-Case 'dirty-build-rejected' -ExpectedError 'Dirty or unknown build candidate' { param($c) Edit-Json (Join-Path $c.Package 'validation.json') { param($o) $o.buildSourceDirty = $true } }
    Invoke-Case 'package-not-pass-rejected' -ExpectedError 'not PASS' { param($c) Edit-Json (Join-Path $c.Package 'validation.json') { param($o) $o.result = 'FAIL' } }
    Invoke-Case 'identity-mismatch-rejected' -ExpectedError 'Identity mismatch' { param($c) Edit-Json (Join-Path $c.Package 'validation.json') { param($o) $o.sourceHash = '9' * 64 } }
    Invoke-Case 'toolchain-lock-mismatch-rejected' -ExpectedError 'toolchain' {
        param($c)
        Write-TestFile (Join-Path $c.Stage 'toolchain-lock.json') '{"tampered":true}'
    }
    Invoke-Case 'inventory-hash-mismatch-rejected' -ExpectedError 'Inventory hash mismatch' { param($c) Write-TestFile (Join-Path $c.Stage 'UnityPlayer.dll') 'tampered' }
    Invoke-Case 'extra-file-rejected' -ExpectedError 'absent from inventory' { param($c) Write-TestFile (Join-Path $c.Stage 'extra.bin') 'x' }
    Invoke-Case 'missing-file-rejected' -ExpectedError 'missing' { param($c) Remove-Item -LiteralPath (Join-Path $c.Stage 'D3D12/D3D12Core.dll') -Force }
    Invoke-Case 'wrong-iscc-hash-rejected' -ExpectedError 'ISCC hash mismatch' { param($c) Edit-Json (Join-Path $c.Root 'Tools/installer/installer-policy.json') { param($o) $o.isccSha256 = '0' * 64 } }
    Invoke-Case 'bad-version-rejected' -ExpectedError 'ReleaseVersion must be' -Version '1.2' { param($c) }
    Invoke-Case 'non-monotonic-version-rejected' -ExpectedError 'not greater than previous' -Version '0.1.0' {
        param($c)
        $prev = Join-Path $c.Root 'prev-report.json'
        Write-TestFile $prev '{"releaseVersion":"0.1.0"}'
        @{ Args = @('-PreviousInstallerReport', $prev) }
    }

    $summary = [ordered]@{
        result = 'PASS'
        testCount = $results.Count
        scope = 'Synthetic Build-Installer wrapper regressions only; no Unity/player/install/hardware acceptance'
        scriptSha256 = Sha (Join-Path $PSScriptRoot 'Build-Installer.ps1')
        issSha256 = Sha (Join-Path $PSScriptRoot 'installer/shooter.iss')
        policySha256 = Sha (Join-Path $PSScriptRoot 'installer/installer-policy.json')
        cases = @($results)
        evidence = $evidence
    }
    ($summary | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding utf8
    Write-Host "PASS: $($results.Count) build-installer wrapper cases; evidence: $evidence"
    exit 0
}
catch {
    Write-Error $_
    exit 1
}

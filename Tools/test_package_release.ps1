[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $root ('Artifacts/tooling-tests/package-release/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $evidence -Force
$results = [Collections.Generic.List[object]]::new()

function Write-TestFile([string]$Path, [string]$Text) {
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force
    [IO.File]::WriteAllText($Path, $Text)
}

function New-SyntheticPackageRoot([string]$CaseRoot, [string]$MapId, [string]$Profile, [string]$ToolchainHash) {
    $payload = @"
{"packageSchema":"shooter.map-package.v1","mapId":"$MapId","visualProfile":"$Profile","toolchainHash":"$ToolchainHash","seed":"0","generatorId":"map.test","generatorVersion":"1.0.0","displayName":"$MapId","validationSummary":"synthetic","dependencies":[]}
"@
    $tmp = Join-Path $CaseRoot ("tmp-" + [Guid]::NewGuid().ToString('N'))
    Write-TestFile $tmp $payload
    $sha = (Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash.ToLowerInvariant()
    Remove-Item -LiteralPath $tmp -Force
    # Rebuild with stable bytes then place under hash path.
    $dir = Join-Path $CaseRoot "Artifacts/map-packages/$($sha.Substring(0,2))/$sha"
    Write-TestFile (Join-Path $dir 'package.json') $payload
    $actual = (Get-FileHash -LiteralPath (Join-Path $dir 'package.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne $sha) { throw "Synthetic package hash drift for $MapId/$Profile" }
    return [pscustomobject]@{ mapId = $MapId; visualProfile = $Profile; packageRootSha256 = $sha; seed = '0'; serverGameplayOnly = ($Profile -ceq 'gameplay') }
}

function New-CaseRoot([string]$Name) {
    $caseRoot = Join-Path $evidence $Name
    $null = New-Item -ItemType Directory -Path (Join-Path $caseRoot 'Tools') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Package-Release.ps1') -Destination (Join-Path $caseRoot 'Tools/Package-Release.ps1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity.Common.ps1') -Destination (Join-Path $caseRoot 'Tools/Unity.Common.ps1')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'release-package-policy.json') -Destination (Join-Path $caseRoot 'Tools/release-package-policy.json')
    $lockText = '{"schemaVersion":1,"editorVersion":"6000.3.28f1","syntheticTestOnly":true}'
    Write-TestFile (Join-Path $caseRoot 'toolchain-lock.json') $lockText
    Write-TestFile (Join-Path $caseRoot 'game-contract-lock.json') '{"schemaVersion":1,"contractVersion":"Game-v1","syntheticTestOnly":true}'
    Write-TestFile (Join-Path $caseRoot 'content-format-lock.json') '{"schemaVersion":1,"contractVersion":"C0-v1","syntheticTestOnly":true}'
    $toolchainHash = (Get-FileHash -LiteralPath (Join-Path $caseRoot 'toolchain-lock.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $packages = [Collections.Generic.List[object]]::new()
    foreach ($map in @('training-compound', 'depot-yard', 'oil-depot')) {
        foreach ($profile in @('gameplay', 'low', 'balanced', 'high')) {
            $packages.Add((New-SyntheticPackageRoot $caseRoot $map $profile $toolchainHash)) | Out-Null
        }
    }
    $index = [ordered]@{ indexSchema = 'shooter.map-package-index.v1'; packages = @($packages) }
    Write-TestFile (Join-Path $caseRoot 'Artifacts/map-packages/index.json') ($index | ConvertTo-Json -Depth 8)
    # ADR-038: Package-Release requires the persisted release catalog beside the index.
    $catalog = [ordered]@{ catalogSchema = 'shooter.release-catalog.v1'; toolchainHash = $toolchainHash; syntheticTestOnly = $true }
    Write-TestFile (Join-Path $caseRoot 'Artifacts/map-packages/catalog.json') ($catalog | ConvertTo-Json -Depth 4)
    # ADR-078 shared weapon package beside the map packages.
    $weaponPayload = '{"packageSchema":"shooter.weapon-package.v1","toolchainHash":"' + $toolchainHash + '","dependencies":[]}'
    $weaponTmp = Join-Path $caseRoot ('tmp-' + [Guid]::NewGuid().ToString('N'))
    Write-TestFile $weaponTmp $weaponPayload
    $weaponSha = (Get-FileHash -LiteralPath $weaponTmp -Algorithm SHA256).Hash.ToLowerInvariant()
    Remove-Item -LiteralPath $weaponTmp -Force
    Write-TestFile (Join-Path $caseRoot "Artifacts/map-packages/$($weaponSha.Substring(0,2))/$weaponSha/package.json") $weaponPayload
    $weaponIndex = [ordered]@{ indexSchema = 'shooter.weapon-package-index.v1'; toolchainHash = $toolchainHash; packageRootSha256 = $weaponSha }
    Write-TestFile (Join-Path $caseRoot 'Artifacts/map-packages/weapons.json') ($weaponIndex | ConvertTo-Json -Depth 4)
    # T089 combat audio bank required by Package-Release.
    $audioDir = Join-Path $caseRoot 'Content/Audio/combat'
    Write-TestFile (Join-Path $audioDir 'manifest.json') '{"bankId":"combat-audio-bank-v3-stereo","sampleRate":48000,"channels":2,"clips":[]}'
    Write-TestFile (Join-Path $audioDir 'rifle_shot_00.wav') 'RIFF-synthetic'
    # ADR-066 UI typeface license beside the package.
    Write-TestFile (Join-Path $caseRoot 'Content/Fonts/OFL-BarlowCondensed.txt') 'SIL Open Font License synthetic'
    return [pscustomobject]@{ Root = $caseRoot; ToolchainHash = $toolchainHash }
}

function New-Build([string]$CaseRoot, [string]$ToolchainHash, [hashtable]$ManifestOverrides) {
    $buildDir = Join-Path $CaseRoot 'build'
    $player = Join-Path $buildDir 'player'
    $null = New-Item -ItemType Directory -Path (Join-Path $player 'Shooter_Data') -Force
    $null = New-Item -ItemType Directory -Path (Join-Path $player 'Shooter_BackUpThisFolder_ButDontShipItWithYourGame/il2cppOutput') -Force
    $null = New-Item -ItemType Directory -Path (Join-Path $player 'Shooter_BurstDebugInformation_DoNotShip') -Force
    Write-TestFile (Join-Path $player 'Shooter.exe') 'synthetic-shooter-exe'
    Write-TestFile (Join-Path $player 'UnityPlayer.dll') 'synthetic-unity-player'
    Write-TestFile (Join-Path $player 'Shooter_Data/boot.config') 'synthetic-boot'
    Write-TestFile (Join-Path $player 'Shooter_Data/leak.pdb') 'should-exclude'
    Write-TestFile (Join-Path $player 'Player.log') 'should-exclude'
    Write-TestFile (Join-Path $player 'user-settings.json') 'should-exclude'
    Write-TestFile (Join-Path $player 'Shooter_BackUpThisFolder_ButDontShipItWithYourGame/il2cppOutput/x.c') 'backup'
    Write-TestFile (Join-Path $player 'Shooter_BurstDebugInformation_DoNotShip/debug.txt') 'burst'
    $manifest = [ordered]@{
        schemaVersion = 1
        result = 'Succeeded'
        target = 'Client'
        sourceCommit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        sourceHash = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
        toolchainHash = $ToolchainHash
        contentManifestHash = 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        editorVersion = '6000.3.28f1'
        editorRevision = '8f082d2fe476'
        backend = 'IL2CPP'
        sourceDirty = $false
        development = $false
        graphicsSourceAssets = @()
        sourceAssetPaths = @()
        totalBytes = 1
    }
    foreach ($key in $ManifestOverrides.Keys) { $manifest[$key] = $ManifestOverrides[$key] }
    Write-TestFile (Join-Path $buildDir 'build-manifest.json') ($manifest | ConvertTo-Json -Depth 8)
    return $buildDir
}

function Invoke-PackageCase([string]$Name, [scriptblock]$Setup, [string]$ExpectedError = '', [switch]$ExpectPass, [switch]$Zip) {
    $created = New-CaseRoot $Name
    $caseRoot = $created.Root
    $buildDir = New-Build $caseRoot $created.ToolchainHash @{}
    $previous = $null
    & $Setup $caseRoot $buildDir $created.ToolchainHash ([ref]$previous)
    $args = @('-NoProfile', '-File', (Join-Path $caseRoot 'Tools/Package-Release.ps1'), '-ClientBuildRunDirectory', $buildDir)
    if ($previous) { $args += @('-PreviousPackageDirectory', $previous) }
    if ($Zip) { $args += '-Zip' }
    $logPath = Join-Path $caseRoot 'wrapper.log'
    & (Join-Path $PSHOME 'pwsh.exe') @args *> $logPath
    $code = $LASTEXITCODE
    $validation = Get-ChildItem -LiteralPath (Join-Path $caseRoot 'Artifacts/m11-playable-release') -Recurse -Filter validation.json -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (!$validation) { throw "$Name produced no validation.json; see $logPath" }
    $report = Get-Content -LiteralPath $validation.FullName -Raw | ConvertFrom-Json
    if ($ExpectPass) {
        if ($code -ne 0 -or $report.result -cne 'PASS') { throw "$Name expected PASS; exit=$code result=$($report.result); see $logPath" }
        if ($report.releaseAcceptance -cne 'NotClaimed') { throw "$Name must not claim REL acceptance" }
        if ($report.playerLaunchEvidence -cne 'Pending') { throw "$Name must leave player launch Pending" }
        if ($report.sourceCommit -cne 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') { throw "$Name stamped unexpected sourceCommit" }
        $inventory = Get-Content -LiteralPath $report.inventoryPath -Raw | ConvertFrom-Json
        $paths = @($inventory | ForEach-Object { $_.path })
        foreach ($required in @('Shooter.exe', 'CONTROLS.txt', 'LICENSES.txt', 'KNOWN_ISSUES.txt', 'Content/map-packages/index.json', 'Content/Audio/combat/manifest.json')) {
            if ($paths -notcontains $required) { throw "$Name inventory missing $required" }
        }
        foreach ($banned in @('Shooter_BackUpThisFolder_ButDontShipItWithYourGame/il2cppOutput/x.c', 'Shooter_BurstDebugInformation_DoNotShip/debug.txt', 'Shooter_Data/leak.pdb', 'Player.log', 'user-settings.json')) {
            if ($paths -contains $banned) { throw "$Name failed to exclude $banned" }
        }
        if ($report.extractRehearsal -cne 'PASS') { throw "$Name stage rehearsal did not pass" }
        if ([int]$report.stagedHardLinks -lt 1) { throw "$Name staged no hard links (expected same-volume linking)" }
        if ($Zip) {
            if (!(Test-Path -LiteralPath $report.archivePath -PathType Leaf)) { throw "$Name missing archive" }
            if ([string]::IsNullOrWhiteSpace([string]$report.archiveSha256)) { throw "$Name missing archive sha" }
        }
        elseif ($null -ne $report.archivePath) { throw "$Name wrote an archive without -Zip" }
    }
    else {
        if ($code -eq 0 -or $report.result -cne 'FAIL') { throw "$Name expected FAIL; exit=$code result=$($report.result); see $logPath" }
        if ($ExpectedError -and ("$($report.failure)" -notlike "*$ExpectedError*")) {
            throw "$Name failure did not include '$ExpectedError'; was: $($report.failure)"
        }
    }
    $results.Add([ordered]@{ name = $Name; result = 'PASS'; wrapperExitCode = $code; report = $validation.FullName; expectedError = $ExpectedError }) | Out-Null
}

try {
    Invoke-PackageCase 'clean-pass' -ExpectPass {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        # default clean fixture
    }

    Invoke-PackageCase 'zip-pass' -ExpectPass -Zip {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
    }

    Invoke-PackageCase 'dirty-build-rejected' -ExpectedError 'Dirty build candidate rejected' {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        $manifestPath = Join-Path $buildDir 'build-manifest.json'
        $m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $m.sourceDirty = $true
        ($m | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }

    Invoke-PackageCase 'toolchain-mismatch-rejected' -ExpectedError 'Build/toolchain mismatch' {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        $manifestPath = Join-Path $buildDir 'build-manifest.json'
        $m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $m.toolchainHash = 'ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff'
        ($m | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }

    Invoke-PackageCase 'missing-manifest-rejected' -ExpectedError 'Missing build-manifest.json' {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        Remove-Item -LiteralPath (Join-Path $buildDir 'build-manifest.json') -Force
    }

    Invoke-PackageCase 'development-build-rejected' -ExpectedError 'Development builds are rejected' {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        $manifestPath = Join-Path $buildDir 'build-manifest.json'
        $m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $m.development = $true
        ($m | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }

    Invoke-PackageCase 'missing-content-rejected' -ExpectedError 'Content closure missing required package' {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        $indexPath = Join-Path $caseRoot 'Artifacts/map-packages/index.json'
        $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
        $index.packages = @($index.packages | Where-Object { -not ($_.mapId -ceq 'depot-yard' -and $_.visualProfile -ceq 'high') })
        ($index | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $indexPath -Encoding utf8
    }

    Invoke-PackageCase 'rollback-with-previous' -ExpectPass {
        param($caseRoot, $buildDir, $toolchainHash, [ref]$previous)
        # First create a distinct previous package by running a nested package with alternate commit.
        $prevBuild = Join-Path $caseRoot 'prev-build'
        Copy-Item -LiteralPath $buildDir -Destination $prevBuild -Recurse -Force
        $manifestPath = Join-Path $prevBuild 'build-manifest.json'
        $m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $m.sourceCommit = 'dddddddddddddddddddddddddddddddddddddddd'
        $m.sourceHash = 'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee'
        ($m | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $manifestPath -Encoding utf8
        & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $caseRoot 'Tools/Package-Release.ps1') -ClientBuildRunDirectory $prevBuild *> (Join-Path $caseRoot 'prev-package.log')
        if ($LASTEXITCODE -ne 0) { throw "Failed to create previous package fixture; see prev-package.log" }
        $prevValidation = Get-ChildItem -LiteralPath (Join-Path $caseRoot 'Artifacts/m11-playable-release/dddddddddddddddddddddddddddddddddddddddd') -Recurse -Filter validation.json |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $previous.Value = Split-Path -Parent $prevValidation.FullName
    }

    $summary = [ordered]@{
        result = 'PASS'
        testCount = $results.Count
        scope = 'Synthetic Package-Release wrapper regressions only; no Unity/player/hardware acceptance'
        sourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Package-Release.ps1') -Algorithm SHA256).Hash.ToLowerInvariant()
        policySha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'release-package-policy.json') -Algorithm SHA256).Hash.ToLowerInvariant()
        cases = @($results)
        evidence = $evidence
    }
    ($summary | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath (Join-Path $evidence 'results.json') -Encoding utf8
    Write-Host "PASS: $($results.Count) package-release wrapper cases; evidence: $evidence"
    exit 0
}
catch {
    Write-Error $_
    exit 1
}

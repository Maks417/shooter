[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Synthetic wrapper regression only; does not claim visual or hardware acceptance.
$repoRoot = Split-Path -Parent $PSScriptRoot
$evidence = Join-Path $repoRoot ('Artifacts/tooling-tests/weapon-lod-transition-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $evidence -Force
$python = (Get-Command python -ErrorAction Stop).Source
$analyzer = Join-Path $PSScriptRoot 'weapon_lod_transition.py'
$measure = Join-Path $PSScriptRoot 'Measure-WeaponLodTransition.ps1'
$results = [Collections.Generic.List[object]]::new()

function Invoke-AnalyzerCase([string]$Name, [object]$Report, [bool]$ExpectPass) {
    $caseRoot = Join-Path $evidence $Name
    $null = New-Item -ItemType Directory -Path $caseRoot -Force
    $inputPath = Join-Path $caseRoot 'samples.json'
    $outputPath = Join-Path $caseRoot 'analysis.json'
    $logPath = Join-Path $caseRoot 'analyzer.log'
    ($Report | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $inputPath -Encoding utf8NoBOM
    & $python $analyzer --input $inputPath --output $outputPath *> $logPath
    $code = $LASTEXITCODE
    if (!(Test-Path -LiteralPath $outputPath -PathType Leaf)) { throw "$Name produced no analysis; see $logPath" }
    $summary = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($ExpectPass) {
        if ($code -ne 0 -or $summary.result -cne 'PASS' -or $summary.visualCaptureResult -cne 'Pending') {
            throw "$Name expected PASS with visual Pending; exit=$code result=$($summary.result); see $logPath"
        }
    }
    else {
        if ($code -eq 0 -or $summary.result -cne 'FAIL' -or $summary.visualCaptureResult -cne 'Pending') {
            throw "$Name expected FAIL with visual Pending; exit=$code result=$($summary.result); see $logPath"
        }
    }
    $results.Add([ordered]@{ name = $Name; result = 'PASS'; wrapperExitCode = $code; analysis = $outputPath })
}

try {
    $happyRoot = Join-Path $evidence 'happy-sweep'
    $null = New-Item -ItemType Directory -Path $happyRoot -Force
    $happySamples = Join-Path $happyRoot 'samples.json'
    $happyAnalysis = Join-Path $happyRoot 'analysis.json'
    $happyLog = Join-Path $happyRoot 'analyzer.log'
    & $python $analyzer --emit-sweep $happySamples --output $happyAnalysis *> $happyLog
    if ($LASTEXITCODE -ne 0) { throw "happy-sweep emit failed; see $happyLog" }
    $happy = Get-Content -LiteralPath $happyAnalysis -Raw | ConvertFrom-Json
    if ($happy.result -cne 'PASS' -or $happy.visualCaptureResult -cne 'Pending') {
        throw 'happy-sweep must PASS numeric policy with visual Pending'
    }
    $results.Add([ordered]@{ name = 'happy-sweep'; result = 'PASS'; wrapperExitCode = 0; analysis = $happyAnalysis })

    Invoke-AnalyzerCase 'policy-mismatch' ([ordered]@{
        schemaVersion = 1
        samples = @(
            [ordered]@{ heightFraction = 0.45; selectedLod = 1; previousLod = 0; override = $null }
        )
    }) $false

    Invoke-AnalyzerCase 'flicker-aba' ([ordered]@{
        schemaVersion = 1
        samples = @(
            [ordered]@{ heightFraction = 0.50; selectedLod = 0; previousLod = 0; override = $null }
            [ordered]@{ heightFraction = 0.50; selectedLod = 1; previousLod = 0; override = $null }
            [ordered]@{ heightFraction = 0.50; selectedLod = 0; previousLod = 1; override = $null }
        )
    }) $false

    # Measure wrapper failure path: missing SamplesPath must FAIL fast and keep visual Pending.
    $missingRoot = Join-Path $evidence 'measure-missing-samples'
    $null = New-Item -ItemType Directory -Path $missingRoot -Force
    $missingLog = Join-Path $missingRoot 'measure.log'
    & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $measure -SamplesPath (Join-Path $missingRoot 'does-not-exist.json') -TimeoutSeconds 60 *> $missingLog
    if ($LASTEXITCODE -eq 0) { throw 'Measure wrapper did not fail on missing SamplesPath' }
    $commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    $runParent = Join-Path $repoRoot "Artifacts/m1-procedural-feasibility/$commit"
    $runDir = Get-ChildItem -LiteralPath $runParent -Directory -Filter 'weapon-lod-transition-*' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $runDir) { throw 'Measure missing-path case wrote no evidence directory' }
    $run = Get-Content -LiteralPath (Join-Path $runDir.FullName 'run.json') -Raw | ConvertFrom-Json
    if ($run.result -cne 'FAIL' -or $run.visualCaptureResult -cne 'Pending') {
        throw 'Measure missing-path run.json must FAIL with visual Pending'
    }
    if (-not [string]$run.failure) { throw 'Measure missing-path run.json must record failure' }
    $results.Add([ordered]@{ name = 'measure-missing-samples'; result = 'PASS'; wrapperExitCode = 1; analysis = (Join-Path $runDir.FullName 'run.json') })

    # Measure wrapper analysis contract with good/bad external sample files (analyzer path used by Measure).
    foreach ($case in @(
        @{ name = 'measure-good-samples'; expectPass = $true; mutate = { param($p) } },
        @{ name = 'measure-bad-samples'; expectPass = $false; mutate = { param($p) $p.samples[0].selectedLod = (($p.samples[0].selectedLod + 1) % 4) } }
    )) {
        $caseRoot = Join-Path $evidence $case.name
        $null = New-Item -ItemType Directory -Path $caseRoot -Force
        $baseSweep = Join-Path $caseRoot 'base-sweep.json'
        $null = & $python $analyzer --emit-sweep $baseSweep --output (Join-Path $caseRoot 'base-analysis.json')
        if ($LASTEXITCODE -ne 0) { throw "$($case.name) could not emit base sweep" }
        $payload = Get-Content -LiteralPath $baseSweep -Raw | ConvertFrom-Json
        & $case.mutate $payload
        $samplesPath = Join-Path $caseRoot 'external-samples.json'
        ($payload | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $samplesPath -Encoding utf8NoBOM
        $analysisOut = Join-Path $caseRoot 'measure-analysis.json'
        $measureLog = Join-Path $caseRoot 'measure-analyzepath.log'
        & $python $analyzer --input $samplesPath --output $analysisOut *> $measureLog
        $code = $LASTEXITCODE
        $summary = Get-Content -LiteralPath $analysisOut -Raw | ConvertFrom-Json
        if ($case.expectPass) {
            if ($code -ne 0 -or $summary.result -cne 'PASS') { throw "$($case.name) expected PASS; see $measureLog" }
        }
        else {
            if ($code -eq 0 -or $summary.result -cne 'FAIL') { throw "$($case.name) expected FAIL; see $measureLog" }
        }
        $results.Add([ordered]@{ name = $case.name; result = 'PASS'; wrapperExitCode = $code; analysis = $analysisOut })
    }

    $summaryPath = Join-Path $evidence 'summary.json'
    [ordered]@{
        result = 'PASS'
        caseCount = $results.Count
        cases = $results
        note = 'Wrapper/analyzer regression only; visual capture remains Pending until Unity wiring.'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM
    Write-Host "PASS: $($results.Count) weapon LOD transition wrapper cases. Evidence: $evidence"
}
catch {
    Write-Warning $_.Exception.Message
    [ordered]@{ result = 'FAIL'; error = $_.Exception.Message; cases = $results } |
        ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding utf8NoBOM
    exit 1
}

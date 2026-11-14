[CmdletBinding()]
param(
    [string]$CaptureRoot = '',
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$python = (Get-Command python -ErrorAction Stop).Source
$module = Join-Path $PSScriptRoot 'product_visual_defect_inventory.py'
if (!(Test-Path -LiteralPath $module -PathType Leaf)) { throw "Missing $module" }

$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
$sourceDirty = [bool](& git -C $repoRoot status --porcelain)
$utc = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$packageRoot = Join-Path $repoRoot "Artifacts/m8-runtime-composition/$sourceCommit/visual-defect-inventory-$utc"
$null = New-Item -ItemType Directory -Path $packageRoot -Force

$run = [ordered]@{
    schemaVersion = 1
    action = 'inventory-product-visual-defects'
    taskId = 'T062'
    fixId = 'FIX11'
    scope = 'Initial capture/overlay tooling + honest defect inventory. No P05 clearance search.'
    sourceCommit = $sourceCommit
    sourceDirty = $sourceDirty
    packageRoot = $packageRoot
    visualAcceptance = 'FAIL'
    fourthClearanceSearchAuthorized = $false
    disposition = 'InventoryOnly'
    fix11Status = 'ToolingReady'
    rel03Status = 'Open'
    captureRoot = $null
    result = 'FAIL'
    failure = $null
    note = 'P05 remains FAIL under ADR-024. Failed artifacts must not be deleted or relabeled PASS.'
}

function Write-RunReport {
    $run | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot 'run.json') -Encoding utf8NoBOM
}

try {
    if ($DryRun) {
        $run.note = 'Dry-run inventory package (no live product captures attached).'
    }
    if ($CaptureRoot) {
        $resolved = [IO.Path]::GetFullPath($CaptureRoot)
        if (!(Test-Path -LiteralPath $resolved -PathType Container)) {
            throw "CaptureRoot not found: $resolved"
        }
        $run.captureRoot = $resolved
    }

    $argList = @(
        $module,
        '--output-dir', $packageRoot,
        '--source-commit', $sourceCommit,
        '--source-dirty', ($(if ($sourceDirty) { 'true' } else { 'false' }))
    )
    if ($run.captureRoot) {
        $argList += @('--capture-root', [string]$run.captureRoot)
    }
    $logPath = Join-Path $packageRoot 'inventory.log'
    & $python @argList 2>&1 | Tee-Object -FilePath $logPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "product_visual_defect_inventory.py failed (exit $LASTEXITCODE). See $logPath" }

    $inventoryPath = Join-Path $packageRoot 'defect-inventory.json'
    $inventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
    if ($inventory.visualAcceptance -cne 'FAIL') { throw 'Inventory must retain visualAcceptance=FAIL' }
    if ($inventory.fourthClearanceSearchAuthorized -ne $false) { throw 'Inventory must keep fourthClearanceSearchAuthorized=false' }
    if ($inventory.fix11Status -cne 'ToolingReady') { throw 'Expected fix11Status=ToolingReady' }

    $run.result = 'PASS'
    $run.fix11Status = 'ToolingReady'
    Write-RunReport
    Write-Host "T062 inventory PASS (tooling/inventory only; P05 FAIL retained). Evidence: $packageRoot"
    exit 0
}
catch {
    $run.result = 'FAIL'
    $run.failure = "$_"
    Write-RunReport
    Write-Error $_
    exit 1
}

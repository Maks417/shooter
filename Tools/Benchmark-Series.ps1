[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory,
    [Parameter(Mandatory)][string]$SensorLog,
    [ValidateSet('B01-minimal-v1','B01-materials-v1')][string]$Workload='B01-minimal-v1', [string]$MaterialCacheRoot='',
    [string]$HardwareEvidence='',
    [ValidateRange(0,1800)][int]$PreconditionSeconds=1200)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
if ($Workload -eq 'B01-materials-v1' -and !$MaterialCacheRoot) { throw 'B01-materials-v1 requires -MaterialCacheRoot.' }
if ($Workload -eq 'B01-minimal-v1' -and $MaterialCacheRoot) { throw 'Material cache is valid only for B01-materials-v1.' }
$unity=Get-ShooterUnity
$run=New-ShooterRun 'benchmark-series' $unity
$build=(Resolve-Path -LiteralPath $BuildRunDirectory).Path
$sensor=(Resolve-Path -LiteralPath $SensorLog).Path
function Copy-LiveSensorSnapshot([string]$Destination) {
    $inputStream=[IO.File]::Open($sensor,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $outputStream=$null
    try {
        $remaining=$inputStream.Length
        if ($remaining -gt 64MB) { throw 'Sensor snapshot exceeds the bounded 64 MiB limit.' }
        $outputStream=[IO.File]::Open($Destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
        $buffer=[byte[]]::new(65536)
        while ($remaining -gt 0) {
            $count=$inputStream.Read($buffer,0,[int][Math]::Min($remaining,$buffer.Length))
            if ($count -eq 0) { throw 'Sensor file was truncated during snapshot.' }
            $outputStream.Write($buffer,0,$count);$remaining-=$count
        }
    }
    finally { if ($outputStream) {$outputStream.Dispose()};$inputStream.Dispose() }
}
try {
    $run.Report['scope']='Three prescribed release captures, no retries; additional same-player conditioning precedes run1 warmup. Hardware review remains explicit.'
    $run.Report['buildDirectory']=$build
    $run.Report['workloadId']=$Workload
    $run.Report['sensorLog']=$sensor
    $run.Report['utcOffsetMinutes']=[int][TimeZoneInfo]::Local.GetUtcOffset([DateTime]::UtcNow).TotalMinutes
    $run.Report['timeZone']=[TimeZoneInfo]::Local.Id
    $run.Report['preconditionSeconds']=$PreconditionSeconds
    $run.Report['attempts']=@()
    $run.Report.result='Collecting'
    Save-ShooterRun $run
    Copy-LiveSensorSnapshot (Join-Path $run.Path 'sensor-before.csv')
    $base=Split-Path -Parent $run.Path
    for ($index=1;$index -le 3;$index++) {
        $before=@(Get-ChildItem -LiteralPath $base -Directory -Filter "benchmark-$index-*" | ForEach-Object Name)
        $argsForWrapper=@('-NoProfile','-File',(Join-Path $PSScriptRoot 'Benchmark.ps1'),'-BuildRunDirectory',$build,'-RunIndex',$index,'-PresentMon','-Workload',$Workload)
        if ($MaterialCacheRoot) { $argsForWrapper+=@('-MaterialCacheRoot',(Resolve-Path -LiteralPath $MaterialCacheRoot).Path) }
        if ($index -eq 1) { $argsForWrapper+=@('-PreconditionSeconds',$PreconditionSeconds) }
        if ($HardwareEvidence) { $argsForWrapper+=@('-HardwareEvidence',(Resolve-Path -LiteralPath $HardwareEvidence).Path) }
        $run.Report.commands+= [ordered]@{executable=(Join-Path $PSHOME 'pwsh.exe');arguments=$argsForWrapper}
        Save-ShooterRun $run
        $started=[DateTime]::UtcNow.ToString('o')
        & (Join-Path $PSHOME 'pwsh.exe') @argsForWrapper 2>&1 | Tee-Object -FilePath (Join-Path $run.Path "run-$index-console.log")
        $code=$LASTEXITCODE
        $new=@(Get-ChildItem -LiteralPath $base -Directory -Filter "benchmark-$index-*" | Where-Object Name -NotIn $before)
        if ($new.Count -ne 1) { throw 'Cannot uniquely identify capture from this series.' }
        $attempt=[ordered]@{runIndex=$index;directory=$new[0].Name;wrapperExitCode=$code;startedUtc=$started;endedUtc=[DateTime]::UtcNow.ToString('o')}
        $run.Report.attempts+=$attempt
        Save-ShooterRun $run
        $reportPath=Join-Path $new[0].FullName 'benchmark-result.json'
        if (!(Test-Path -LiteralPath $reportPath)) { throw 'Native capture or analysis failed; series stops without retry.' }
        $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if ($report.gateStatus -eq 'ComputedFail') { throw 'Measured budget failure; series stops without retry.' }
        Write-Host "Completed prescribed capture $index/3: $($report.gateStatus); next capture starts immediately."
    }
    $run.Report.result='PendingHardware'
    $run.Report['collectionComplete']=$true
}
catch { $run.Report.result='FAIL';$run.Report.error=$_.Exception.Message;Write-Warning $run.Report.error }
finally {
    $run.Report['snapshotReadStartedUtc']=[DateTime]::UtcNow.ToString('o')
    try { Copy-LiveSensorSnapshot (Join-Path $run.Path 'sensor-log-snapshot.csv') }
    catch { $run.Report['snapshotError']=$_.Exception.Message;$run.Report.result='FAIL' }
    $run.Report['snapshotReadEndedUtc']=[DateTime]::UtcNow.ToString('o')
    if (Test-Path -LiteralPath (Join-Path $run.Path 'sensor-log-snapshot.csv')) { $run.Report['sensorSnapshotSha256']=Get-ShooterHash (Join-Path $run.Path 'sensor-log-snapshot.csv') }
    Save-ShooterRun $run
}
if ($run.Report.result -ne 'PASS') { exit 1 }

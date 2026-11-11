[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildRunDirectory,
    [ValidateSet('B01-minimal-v1')][string]$Workload = 'B01-minimal-v1',
    [ValidateSet('Low')][string]$Profile = 'Low', [ValidateRange(1,3)][int]$RunIndex = 1, [switch]$Diagnostic)
. (Join-Path $PSScriptRoot 'Unity.Common.ps1')
$unity = Get-ShooterUnity
$buildRoot = (Resolve-Path -LiteralPath $BuildRunDirectory).Path
$buildManifest = Join-Path $buildRoot 'build-manifest.json'
$build = Get-Content -LiteralPath $buildManifest -Raw | ConvertFrom-Json
if ($build.result -cne 'Succeeded' -or $build.target -cne 'Client' -or $build.backend -cne 'IL2CPP' -or $build.development -or $build.toolchainHash -cne $unity.LockHash) { throw 'A matching release IL2CPP client build is required.' }
if ((Get-ShooterHash (Join-Path $buildRoot 'harness-manifest.json')) -cne $build.contentManifestHash) { throw 'Harness content manifest hash mismatch.' }
$run = New-ShooterRun $(if ($Diagnostic) { 'benchmark-diagnostic' } else { "benchmark-$RunIndex" }) $unity
try {
    Assert-ShooterPackedProvenance $build
    $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name.Trim()
    $ram = @(Get-CimInstance Win32_PhysicalMemory)
    $video = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion)
    $power = (powercfg /getactivescheme | Out-String).Trim()
    $battery = @(Get-CimInstance -Namespace root/WMI -ClassName BatteryStatus -ErrorAction SilentlyContinue)
    $systemDisk = Get-Partition -DriveLetter C -ErrorAction SilentlyContinue | Get-Disk -ErrorAction SilentlyContinue
    $hardware = [ordered]@{ cpu = $cpu; gpuAdapter = $null; ramBytes = [long](($ram | Measure-Object Capacity -Sum).Sum); ramSpeed = [int]$ram[0].ConfiguredClockSpeed;
        channelMode = $null; os = [Environment]::OSVersion.VersionString; driver = ($video | Where-Object Name -match 'AMD.*Radeon' | Select-Object -First 1).DriverVersion;
        powerPlan = $power; acPower = $(if ($battery.Count) { [bool]$battery[0].PowerOnline } else { $null }); thermalStabilized = $false; nvmeVerified = [bool]($systemDisk -and $systemDisk.BusType -eq 'NVMe');
        memorySlots = @($ram | Select-Object BankLabel,DeviceLocator,Capacity,ConfiguredClockSpeed); adapters = $video }
    $hardwarePath = Join-Path $run.Path 'hardware.json'; $hardware | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $hardwarePath -Encoding utf8NoBOM
    $player = Join-Path $buildRoot 'player/Shooter.exe'; $log = Join-Path $run.Path 'player.log'
    $arguments = @('-force-d3d11','-logFile',$log,'-shooterProfile',$Profile,'-shooterBenchmark',$Workload,'-shooterBenchmarkOutput',$run.Path,'-shooterBenchmarkDiagnostic',([bool]$Diagnostic).ToString().ToLowerInvariant())
    Invoke-ShooterProcess -Executable $player -ProcessArguments $arguments -WorkingDirectory (Split-Path -Parent $player) -LogPath (Join-Path $run.Path 'process.log') -MemorySamplePath (Join-Path $run.Path 'memory.csv') -TimeoutSeconds $(if ($Diagnostic) { 60 } else { 330 })
    if (!(Test-Path -LiteralPath $log) -or !(Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_BENCHMARK_COMPLETE' -Quiet) -or (Select-String -LiteralPath $log -SimpleMatch 'SHOOTER_FAILURE' -Quiet)) { throw 'Player capture failed or did not finish.' }
    $output = Join-Path $run.Path $(if ($Diagnostic) { 'diagnostic-result.json' } else { 'benchmark-result.json' })
    & python (Join-Path $PSScriptRoot 'analyze_benchmark.py') --capture $run.Path --build-manifest $buildManifest --hardware $hardwarePath --run-index $RunIndex --output $output
    if ($LASTEXITCODE -ne 0) { throw 'Capture analysis failed.' }
    $result = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    $run.Report['gateStatus'] = $result.gateStatus
    $run.Report['scope'] = $(if ($Diagnostic) { 'Short diagnostic only; not benchmark acceptance' } else { 'Real B01 capture; hardware/reviewer gate remains explicit in result' })
    $run.Report.result = 'PASS'
    if (!$Diagnostic -and $result.gateStatus -ne 'ComputedPass') { $run.Report.result = $result.gateStatus }
}
catch { $run.Report.error = $_.Exception.Message; Write-Warning $run.Report.error }
finally { Save-ShooterRun $run }
if ($run.Report.result -ne 'PASS') { exit 1 }

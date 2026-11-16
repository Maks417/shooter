<#
.SYNOPSIS
  Review-only UI capture: launches a built client windowed with the repository map packages, waits, then
  screenshots the player window after each scripted key. Diagnostic evidence only, never acceptance.

.DESCRIPTION
  Used for the ADR-084..086 UI reviews. It takes over the desktop for the run: the player window is brought to
  the foreground before each key and each capture (CopyFromScreen; PrintWindow returns stale D3D frames).

  Keys use SendKeys syntax ('{ENTER}', '+{TAB}', '{DOWN}', '{F15}' as a no-op wait). Unity's Input System reads raw
  input, so held keys need a virtual-key and scan code: 'HOLD:<vk>,<scan>' holds for ~0.9 s and captures mid-hold
  (scoreboard = 'HOLD:9,15' for Tab).

  Useful flags passed with -Extra:
    -shooterUiGallery 1        element gallery instead of Main (medals, ribbons, icons, controls)
    -shooterUiGallery results  debrief preview over sample rows (loops every 7 s)

  Into a match from Main: '{ENTER}','+{TAB}','+{TAB}','{ENTER}' (Play → setup → START MATCH), then waits.

.EXAMPLE
  ./Tools/Capture-UiReview.ps1 -BuildRunDirectory <build-client run> -OutDir Artifacts/visual-design/<id>/<run> `
    -Keys '{ENTER}','+{TAB}','+{TAB}','{ENTER}','{F15}','HOLD:9,15'
#>
param(
    [Parameter(Mandatory)][string]$BuildRunDirectory,
    [Parameter(Mandatory)][string]$OutDir,
    [int]$WaitSeconds = 20,
    [int]$StepSeconds = 2,
    [string[]]$Keys = @(),
    [string[]]$Extra = @(),
    [string]$MapCacheRoot = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$player = Join-Path $BuildRunDirectory 'player/Shooter.exe'
if (!(Test-Path -LiteralPath $player -PathType Leaf)) { throw "Player not found: $player" }
if (!$MapCacheRoot) { $MapCacheRoot = Join-Path $root 'Artifacts/map-packages' }
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
if (-not ('ShooterCaptureWin' -as [type])) {
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class ShooterCaptureWin {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
$log = Join-Path $OutDir 'player.log'
$arguments = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', $log,
    '-shooterMapCache', $MapCacheRoot, '-shooterToolchainLock', (Join-Path $root 'toolchain-lock.json')) + $Extra
$p = Start-Process -FilePath $player -WorkingDirectory (Split-Path -Parent $player) -PassThru -ArgumentList $arguments
try {
    Start-Sleep -Seconds $WaitSeconds
    $p.Refresh()
    $h = $p.MainWindowHandle
    if ($h -eq [IntPtr]::Zero) { throw 'Player window not found.' }
    function Shot([string]$name) {
        [ShooterCaptureWin]::SetForegroundWindow($h) | Out-Null
        Start-Sleep -Milliseconds 400
        $r = New-Object ShooterCaptureWin+RECT
        [ShooterCaptureWin]::GetWindowRect($h, [ref]$r) | Out-Null
        $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size); $g.Dispose()
        $path = Join-Path $OutDir ($name + '.png'); $bmp.Save($path); $bmp.Dispose(); $path
    }
    Shot 'main'
    $i = 0
    foreach ($k in $Keys) {
        [ShooterCaptureWin]::SetForegroundWindow($h) | Out-Null
        Start-Sleep -Milliseconds 300
        $i++
        if ($k -like 'HOLD:*') {
            $parts = $k.Substring(5).Split(',')
            $vk = [byte][int]$parts[0]; $sc = [byte][int]$parts[1]
            [ShooterCaptureWin]::keybd_event($vk, $sc, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 900
            Shot ("step$i")
            [ShooterCaptureWin]::keybd_event($vk, $sc, 2, [UIntPtr]::Zero)
            continue
        }
        [System.Windows.Forms.SendKeys]::SendWait($k)
        Start-Sleep -Seconds $StepSeconds
        Shot ("step$i")
    }
} finally {
    if (-not $p.HasExited) { $p.Kill() }
}

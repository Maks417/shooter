<#
.SYNOPSIS
  Review-only first-person recording: launches a built client windowed, enters a match from Main, then records the
  window to MP4 (ffmpeg gdigrab) while a scripted timeline of real keyboard and mouse input drives the local player.
  Diagnostic evidence only, never acceptance (ADR-129 follow-up).

.DESCRIPTION
  It takes over the desktop for the run (about 1.5 minutes): the player window is kept in the foreground and input is
  sent with SendInput scan codes and mouse buttons, which Unity's Input System reads as raw input.

  Output in -OutDir:
    viewmodel.mp4      the whole timeline at 30 fps
    timeline.json      each step's label and start/end seconds in the video
    frames/*.png       stills at fixed fractions of each step
    sheet.png          contact sheet of those stills, labelled
    player.log         the player's log

  Requires ffmpeg on PATH. A bot may kill the player mid-run; check the sheet and run again if so. Running a match
  uses the normal per-user settings folder; the player is killed before the match ends, so no result is recorded.

.EXAMPLE
  ./Tools/Record-ViewModel.ps1 -BuildRunDirectory <build-client run> -OutDir Artifacts/visual-design/adr-129-fp-hands/<run>/before
#>
param(
    [Parameter(Mandatory)][string]$BuildRunDirectory,
    [Parameter(Mandatory)][string]$OutDir,
    [int]$Width = 1600,
    [int]$Height = 900,
    [int]$LoadSeconds = 25,
    [string[]]$MenuKeys = @('{ENTER}', '+{TAB}', '+{TAB}', '{ENTER}'),
    [string[]]$Extra = @(),
    [string]$MapCacheRoot = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$player = Join-Path $BuildRunDirectory 'player/Shooter.exe'
if (!(Test-Path -LiteralPath $player -PathType Leaf)) { throw "Player not found: $player" }
$ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if (!$ffmpeg) { throw 'ffmpeg not found on PATH.' }
if (!$MapCacheRoot) { $MapCacheRoot = Join-Path $root 'Artifacts/map-packages' }
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
if (-not ('ShooterRecordWin' -as [type])) {
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class ShooterRecordWin {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
  [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION u; }
  [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  // Scan-code keys (KEYEVENTF_SCANCODE = 8, KEYUP = 2, EXTENDED = 1).
  public static void Key(ushort scan, bool down, bool extended) {
    var i = new INPUT { type = 1 };
    i.u.ki.scan = scan; i.u.ki.flags = 8u | (down ? 0u : 2u) | (extended ? 1u : 0u);
    SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
  }
  // Mouse buttons: left down/up = 2/4, right down/up = 8/16.
  public static void Mouse(bool right, bool down) {
    var i = new INPUT { type = 0 };
    i.u.mi.flags = right ? (down ? 8u : 16u) : (down ? 2u : 4u);
    SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
  }
  // Relative mouse move (MOUSEEVENTF_MOVE = 1).
  public static void Move(int dx, int dy) {
    var i = new INPUT { type = 0 };
    i.u.mi.dx = dx; i.u.mi.dy = dy; i.u.mi.flags = 1u;
    SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
  }
}
"@
}
# Scan codes for the default bindings (GameData.DefaultBindings).
$Scan = @{ W = 0x11; Shift = 0x2A; R = 0x13; D1 = 0x02; D2 = 0x03; D3 = 0x04; D4 = 0x05; Ctrl = 0x1D }
function Press([string]$what, [bool]$down) {
    switch ($what) {
        'RMB' { [ShooterRecordWin]::Mouse($true, $down) }
        'LMB' { [ShooterRecordWin]::Mouse($false, $down) }
        default { [ShooterRecordWin]::Key([uint16]$Scan[$what], $down, $false) }
    }
}

# The timeline: each step holds its keys for its whole duration; 'Taps' presses LMB that many times evenly.
$timeline = @(
    @{ Label = 'rifle-idle'; Seconds = 2.5; Hold = @() }
    @{ Label = 'rifle-ads'; Seconds = 2.5; Hold = @('RMB') }
    @{ Label = 'rifle-fire'; Seconds = 1.2; Hold = @('LMB') }
    @{ Label = 'rifle-walk'; Seconds = 2.0; Hold = @('W') }
    @{ Label = 'rifle-sprint'; Seconds = 3.0; Hold = @('W', 'Shift') }
    @{ Label = 'rifle-reload'; Seconds = 3.5; Tap = 'R' }
    @{ Label = 'rifle-empty-fire'; Seconds = 4.0; Hold = @('LMB') }
    @{ Label = 'rifle-empty-reload'; Seconds = 4.0; Tap = 'R' }
    @{ Label = 'rifle-sprint-2'; Seconds = 2.5; Hold = @('W', 'Shift') }
    @{ Label = 'pistol-draw'; Seconds = 1.5; Tap = 'D2' }
    @{ Label = 'pistol-idle'; Seconds = 2.0; Hold = @() }
    @{ Label = 'pistol-ads'; Seconds = 2.0; Hold = @('RMB') }
    @{ Label = 'pistol-walk'; Seconds = 2.0; Hold = @('W') }
    @{ Label = 'pistol-sprint'; Seconds = 3.0; Hold = @('W', 'Shift') }
    @{ Label = 'pistol-reload'; Seconds = 3.0; Tap = 'R' }
    @{ Label = 'pistol-empty-fire'; Seconds = 5.0; Taps = 20 }
    @{ Label = 'pistol-empty-reload'; Seconds = 3.5; Tap = 'R' }
    @{ Label = 'knife'; Seconds = 2.5; Tap = 'D3' }
    @{ Label = 'grenade'; Seconds = 2.5; Tap = 'D4' }
    @{ Label = 'rifle-back'; Seconds = 2.0; Tap = 'D1' }
)
$stillFractions = @(.15, .5, .85)

New-Item -ItemType Directory -Force $OutDir | Out-Null
# Absolute: the player runs from its own folder, so a relative -logFile would land there.
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path
$framesDir = Join-Path $OutDir 'frames'
New-Item -ItemType Directory -Force $framesDir | Out-Null
$log = Join-Path $OutDir 'player.log'
$video = Join-Path $OutDir 'viewmodel.mp4'
$arguments = @('-screen-fullscreen', '0', '-screen-width', "$Width", '-screen-height', "$Height", '-logFile', $log,
    '-shooterMapCache', $MapCacheRoot, '-shooterToolchainLock', (Join-Path $root 'toolchain-lock.json')) + $Extra
$p = Start-Process -FilePath $player -WorkingDirectory (Split-Path -Parent $player) -PassThru -ArgumentList $arguments
$recorder = $null
$steps = @()
try {
    $deadline = (Get-Date).AddSeconds(30)
    do { Start-Sleep -Milliseconds 500; $p.Refresh(); $h = $p.MainWindowHandle } while ($h -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)
    if ($h -eq [IntPtr]::Zero) { throw 'Player window not found.' }
    Start-Sleep -Seconds 8
    foreach ($k in $MenuKeys) {
        [ShooterRecordWin]::SetForegroundWindow($h) | Out-Null
        Start-Sleep -Milliseconds 400
        [System.Windows.Forms.SendKeys]::SendWait($k)
        Start-Sleep -Milliseconds 1200
    }
    Start-Sleep -Seconds $LoadSeconds
    [ShooterRecordWin]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 500

    $rect = New-Object ShooterRecordWin+RECT
    [ShooterRecordWin]::GetClientRect($h, [ref]$rect) | Out-Null
    $origin = New-Object ShooterRecordWin+POINT
    [ShooterRecordWin]::ClientToScreen($h, [ref]$origin) | Out-Null
    $w = ($rect.R - $rect.L) -band -2; $hgt = ($rect.B - $rect.T) -band -2
    [ShooterRecordWin]::SetCursorPos($origin.X + [int]($w / 2), $origin.Y + [int]($hgt / 2)) | Out-Null

    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $ffmpeg
    $info.Arguments = "-hide_banner -loglevel error -y -f gdigrab -framerate 30 -draw_mouse 0 -offset_x $($origin.X) -offset_y $($origin.Y) -video_size ${w}x${hgt} -i desktop -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p `"$video`""
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.CreateNoWindow = $true
    $recorder = [System.Diagnostics.Process]::Start($info)
    Start-Sleep -Milliseconds 1500 # ffmpeg start-up; the video clock starts here
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $videoLead = 1.0

    foreach ($step in $timeline) {
        [ShooterRecordWin]::SetForegroundWindow($h) | Out-Null
        $start = $clock.Elapsed.TotalSeconds + $videoLead
        $hold = @($step.Hold | Where-Object { $_ })
        foreach ($k in $hold) { Press $k $true; Start-Sleep -Milliseconds 30 }
        if ($step.Tap) { Press $step.Tap $true; Start-Sleep -Milliseconds 80; Press $step.Tap $false }
        if ($step.Taps) {
            $gap = [int](1000 * $step.Seconds / $step.Taps)
            for ($i = 0; $i -lt $step.Taps; $i++) { Press 'LMB' $true; Start-Sleep -Milliseconds 60; Press 'LMB' $false; Start-Sleep -Milliseconds ([Math]::Max(10, $gap - 60)) }
        } else {
            $target = $start - $videoLead + $step.Seconds
            while ($clock.Elapsed.TotalSeconds -lt $target) { Start-Sleep -Milliseconds 20 }
        }
        foreach ($k in $hold) { Press $k $false }
        $steps += [pscustomobject]@{ label = $step.Label; start = [Math]::Round($start, 3); end = [Math]::Round($clock.Elapsed.TotalSeconds + $videoLead, 3) }
        Start-Sleep -Milliseconds 150
    }
    Start-Sleep -Milliseconds 500
} finally {
    foreach ($k in 'RMB', 'LMB', 'W', 'Shift') { try { Press $k $false } catch { } }
    if ($recorder -and -not $recorder.HasExited) {
        $recorder.StandardInput.Write('q'); $recorder.StandardInput.Flush()
        if (-not $recorder.WaitForExit(15000)) { $recorder.Kill() }
    }
    if (-not $p.HasExited) { $p.Kill() }
}
if (!(Test-Path -LiteralPath $video)) { throw 'ffmpeg produced no video.' }
$steps | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutDir 'timeline.json') -Encoding utf8

# Stills and a labelled contact sheet.
$stills = @()
foreach ($s in $steps) {
    $n = 0
    foreach ($f in $stillFractions) {
        $t = $s.start + ($s.end - $s.start) * $f
        $png = Join-Path $framesDir ('{0}-{1}.png' -f $s.label, $n)
        & $ffmpeg -hide_banner -loglevel error -y -ss ([string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.000}', $t)) -i $video -frames:v 1 $png
        if (Test-Path -LiteralPath $png) { $stills += [pscustomobject]@{ Path = $png; Label = ('{0} {1:0.0}s' -f $s.label, $t) } }
        $n++
    }
}
if ($stills.Count -gt 0) {
    $cols = $stillFractions.Count; $cellW = 480; $cellH = 270
    $rows = [Math]::Ceiling($stills.Count / $cols)
    $sheet = New-Object System.Drawing.Bitmap ($cols * $cellW), ($rows * $cellH)
    $g = [System.Drawing.Graphics]::FromImage($sheet)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $font = New-Object System.Drawing.Font 'Consolas', 11
    for ($i = 0; $i -lt $stills.Count; $i++) {
        $img = [System.Drawing.Image]::FromFile($stills[$i].Path)
        $x = ($i % $cols) * $cellW; $y = [Math]::Floor($i / $cols) * $cellH
        $g.DrawImage($img, $x, $y, $cellW, $cellH); $img.Dispose()
        $g.FillRectangle([System.Drawing.Brushes]::Black, $x, $y, 260, 18)
        $g.DrawString($stills[$i].Label, $font, [System.Drawing.Brushes]::White, $x + 2, $y + 1)
    }
    $g.Dispose()
    $sheet.Save((Join-Path $OutDir 'sheet.png')); $sheet.Dispose()
}
Write-Output "Recorded $video ($($steps.Count) steps, $($stills.Count) stills)."

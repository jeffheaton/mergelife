#Requires -Version 5.1
# windows-selfcheck.ps1 -- run the built Windows player headless and gate on
# its determinism self-check. The Windows sibling of tools/macos-selfcheck.sh.
#
# Windows Build Support is not installed on the Mac, so CIBuild.Windows runs
# on Jeff's Parallels Win11 VM against a checkout of this repo, writing
# build\windows-x64\HeatonCA.exe (docs\windows-build.md is the full runbook).
# Run this script there afterward:
#
#   powershell -ExecutionPolicy Bypass -File tools\windows-selfcheck.ps1
#
# It launches
#
#   HeatonCA.exe -batchmode -nographics -selfcheck -logFile build\logs\win-selfcheck.log
#
# waits for the player to exit (TimeoutSec, default 120; the process is killed
# at the deadline), then reads the log. AppController honors -selfcheck by
# running DeterminismSelfCheck, logging "[HeatonCA] SELF-CHECK PASS|FAIL" plus
# the report, and calling Application.Quit(passed ? 0 : 1). As on macOS, two
# things must agree for a pass: the player's exit code is 0 AND the log holds
# the PASS line. The verdict rule mirrors tools/check-selfcheck.sh.
#
#   -BuildDir <dir>    folder holding HeatonCA.exe (default build\windows-x64)
#   -LogFile <file>    player log to write and read (default build\logs\win-selfcheck.log)
#   -TimeoutSec <n>    seconds to wait for the player to exit (default 120, 1..3600)
#   -Graphics          omit -nographics (the escape hatch UNITY_GATE_GRAPHICS=1 is
#                      for unity-gate.sh: use it only if a driverless VM turns out
#                      to refuse the null graphics device)
#
# Exit codes (the same ladder as tools/macos-selfcheck.sh):
#   0  player exited 0 and the log holds "[HeatonCA] SELF-CHECK PASS"
#   1  self-check FAIL (the player's own exit code when it is 1..255, else 1)
#   2  no verdict: the player did not exit within TimeoutSec, or exited 0 without
#      logging one
#   3  usage error, or the player at -BuildDir is missing or incomplete (run
#      CIBuild.Windows first)
#   5  environment problem: not Windows, or the player could not be started
#
# Why System.Diagnostics.Process and not `& $Exe` or Start-Process: the Unity
# player is a GUI-subsystem executable, so a console that launches it gets its
# prompt back at once and $LASTEXITCODE means nothing -- only a held process
# handle can report the real exit code. It also keeps the argument string
# verbatim and never routes through the shell (no ExecutionPolicy, no PATH).
# Nothing is redirected, so there is no stdout-drain deadlock; a Windows GUI
# app writes nothing to the inherited console anyway, which is why the verdict
# has to come out of the log file.
#
# Notes for the VM: the unsigned player trips SmartScreen only when launched
# from Explorer, not from a console; the first launch may also raise a Windows
# Firewall prompt for the Unity player, which this headless run does not need
# (deny it). PlayerPrefs land under HKCU\Software\Jeff Heaton\HeatonCA and the
# default Player.log under %USERPROFILE%\AppData\LocalLow\Jeff Heaton\HeatonCA,
# but with -logFile the run's log is the file named here. Keep both the player
# and the log on the VM's own disk: a Parallels shared folder (\\Mac\...) is
# slow and has bitten Unity file writes before.
[CmdletBinding()]
param(
    [string]$BuildDir,
    [string]$LogFile,
    [int]$TimeoutSec = 120,
    [switch]$Graphics
)
$ErrorActionPreference = 'Stop'

# Progress and the PASS verdict go to stdout; every failure goes to stderr, the
# same split as macos-selfcheck.sh. Write-Output rather than Write-Host so a
# caller can capture the transcript either through PowerShell redirection or
# through the process's stdout handle (`powershell -File ... > out.txt`);
# Write-Host in 5.1 reaches neither.
function Write-Err([string]$Message) { [Console]::Error.WriteLine($Message) }
function Write-Note([string]$Message) { Write-Output $Message }

function Exit-Usage([string]$Message)
{
    Write-Err "windows-selfcheck: $Message"
    Write-Err 'usage: powershell -ExecutionPolicy Bypass -File tools\windows-selfcheck.ps1 [-BuildDir <dir>] [-LogFile <file>] [-TimeoutSec <n>] [-Graphics]'
    Write-Err 'exit:  0 PASS, 1 FAIL, 2 no verdict, 3 usage or player missing, 5 environment'
    exit 3
}

# $IsWindows exists from PowerShell 6 on; in 5.1 the variable is absent, and
# 5.1 only ever runs on Windows.
$onWindows = $true
$isWindowsVar = Get-Variable -Name 'IsWindows' -ValueOnly -ErrorAction SilentlyContinue
if ($null -ne $isWindowsVar) { $onWindows = [bool]$isWindowsVar }
if (-not $onWindows) {
    Write-Err 'windows-selfcheck: this gate runs the Windows player and needs Windows (see docs/windows-build.md)'
    exit 5
}
if ($TimeoutSec -lt 1 -or $TimeoutSec -gt 3600) {
    Exit-Usage "-TimeoutSec must be between 1 and 3600 (got $TimeoutSec)"
}

# Absolute paths up front: a relative -LogFile with no directory part used to
# break Split-Path -Parent (it returns an empty string, which New-Item rejects),
# and the player resolves its own relative paths against its working directory,
# not the caller's.
$Project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
function Resolve-Under([string]$Path, [string]$Base)
{
    if (-not [System.IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $Base $Path }
    [System.IO.Path]::GetFullPath($Path)
}

if (-not $BuildDir) { $BuildDir = Join-Path $Project 'build\windows-x64' }
if (-not $LogFile) { $LogFile = Join-Path $Project 'build\logs\win-selfcheck.log' }
$BuildDir = Resolve-Under $BuildDir $Project
$LogFile = Resolve-Under $LogFile $Project

$Exe = Join-Path $BuildDir 'HeatonCA.exe'
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
    Write-Err "windows-selfcheck: no player at $Exe; run CIBuild.Windows first (or pass -BuildDir)"
    exit 3
}
# A player folder missing UnityPlayer.dll or its data folder starts and dies
# with an empty log, which reads exactly like a determinism failure. Name the
# real problem instead. The data folder is named after the executable, so it is
# derived rather than hard-coded.
$DataDir = [System.IO.Path]::GetFileNameWithoutExtension($Exe) + '_Data'
foreach ($part in 'UnityPlayer.dll', $DataDir) {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDir $part))) {
        Write-Err "windows-selfcheck: player incomplete: $BuildDir\$part is missing; rebuild with CIBuild.Windows"
        exit 3
    }
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null
Remove-Item -LiteralPath $LogFile -Force -ErrorAction SilentlyContinue   # never read a previous run's verdict

$PassToken = '[HeatonCA] SELF-CHECK PASS'
$FailToken = 'SELF-CHECK FAIL'
# Same shape as check-selfcheck.sh: the verdict, the report header, and
# "PASS <name>" / "FAIL <name>[: detail]" lines.
$ReportRegex = 'HeatonCA determinism self-check:|\[HeatonCA\] SELF-CHECK|(^|[^A-Za-z0-9_-])(PASS|FAIL) [a-z0-9][a-z0-9-]*(:|\s*$)'

$PlayerArgs = @('-batchmode')
if (-not $Graphics) { $PlayerArgs += '-nographics' }
$PlayerArgs += @('-selfcheck', '-logFile', ('"' + $LogFile + '"'))
$ArgLine = $PlayerArgs -join ' '

Write-Note "windows-selfcheck: exe=$Exe"
Write-Note "windows-selfcheck: log=$LogFile"
Write-Note "windows-selfcheck: running: `"$Exe`" $ArgLine"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $Exe
$psi.Arguments = $ArgLine
$psi.WorkingDirectory = $BuildDir
$psi.UseShellExecute = $false
try {
    $proc = [System.Diagnostics.Process]::Start($psi)
}
catch {
    # Start throws (Win32Exception) rather than returning $null: a blocked
    # binary, a missing VC++ runtime, or an exe whose bitness the OS refuses.
    Write-Err "windows-selfcheck: the player could not be started: $($_.Exception.Message)"
    exit 5
}
if (-not $proc) {
    Write-Err 'windows-selfcheck: the player could not be started'
    exit 5
}

$watch = [System.Diagnostics.Stopwatch]::StartNew()
$timedOut = $false
if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
    $timedOut = $true
    Write-Err "windows-selfcheck: player pid $($proc.Id) still running after $TimeoutSec s; killing it"
    try { $proc.Kill() } catch { }
    $proc.WaitForExit(5000) | Out-Null
}
# ExitCode throws while a process is still alive, which a Kill that did not take
# would cause; the timeout branch below reports the verdict either way.
$playerExit = -1
try { $playerExit = $proc.ExitCode } catch { }
$elapsed = [int]$watch.Elapsed.TotalSeconds

# The log may still be flushing for a moment after the process ends, and the
# handle can stay briefly locked, so both the appearance and the read retry.
$deadline = (Get-Date).AddSeconds(5)
while (-not (Test-Path -LiteralPath $LogFile) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 250
}
$lines = @()
for ($attempt = 1; $attempt -le 5; $attempt++) {
    if (-not (Test-Path -LiteralPath $LogFile)) { break }
    try {
        $lines = @(Get-Content -LiteralPath $LogFile -Encoding UTF8)
        break
    }
    catch {
        Start-Sleep -Milliseconds 250   # the player has not let go of the handle yet
    }
}
foreach ($line in $lines) {
    $text = $line.TrimEnd()   # a stray CR would defeat the regex's end anchor
    if ($text -match $ReportRegex) { Write-Note $text }
}
# Contains, not -match: the verdict comparison is case-sensitive, as in
# check-selfcheck.sh, and a log holding both verdicts is a failure.
$sawFail = @($lines | Where-Object { $_.Contains($FailToken) }).Count -gt 0
$sawPass = @($lines | Where-Object { $_.Contains($PassToken) }).Count -gt 0
if ($sawFail) { $check = 1 } elseif ($sawPass) { $check = 0 } else { $check = 2 }

if ($timedOut) {
    Write-Err "windows-selfcheck: FAIL: the player did not exit within $TimeoutSec s (log verdict code $check, log $LogFile)"
    exit 2
}
if ($playerExit -ne 0) {
    $hex = '0x{0:X8}' -f $playerExit
    Write-Err "windows-selfcheck: FAIL: player exit=$playerExit ($hex) check=$check elapsed=${elapsed}s log=$LogFile"
    if ($lines.Count -eq 0) {
        Write-Err 'windows-selfcheck: the log is empty; the player never reached Unity (a blocked download still marked with the web mark? try: Unblock-File, or Get-ChildItem -Recurse | Unblock-File)'
    }
    # Crashes exit with an NTSTATUS (0xC0000005 access violation, 0xC0000409
    # stack check) that is negative as an int and whose low byte can even be
    # zero, which a caller comparing against 0 would read as a pass. Only a
    # plain 1..255 is passed through; anything else is reported as 1.
    if ($playerExit -ge 1 -and $playerExit -le 255) { exit $playerExit }
    exit 1
}
switch ($check) {
    0 { Write-Note "windows-selfcheck: PASS player exit=0 check=0 elapsed=${elapsed}s log=$LogFile" }
    1 { Write-Err "windows-selfcheck: FAIL: the player exited 0 but the log says SELF-CHECK FAIL (elapsed ${elapsed}s, log $LogFile)" }
    default { Write-Err "windows-selfcheck: FAIL: the player exited 0 but logged no verdict (elapsed ${elapsed}s, log $LogFile)" }
}
exit $check

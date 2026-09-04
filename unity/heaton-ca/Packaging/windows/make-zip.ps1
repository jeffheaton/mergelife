#Requires -Version 5.1
# make-zip.ps1 -- turn the Unity Windows player into the release zip.
#
# Windows is HeatonCA's only unsigned, storeless channel: there is no MSIX and
# no installer, so the deliverable is one zip that a person downloads, unblocks,
# extracts and runs (docs\windows-build.md is the runbook; the Microsoft Store
# was deliberately left out of scope). That makes this script the last gate
# before the bytes go public, so it refuses to package anything it cannot
# vouch for:
#
#   1. the player is COMPLETE -- a folder missing UnityPlayer.dll or the data
#      folder zips without complaint and fails only on the downloader's machine;
#   2. the player is the MONO build the project settings promise (scriptingBackend
#      overrides only Android), because switching Standalone to IL2CPP re-arms the
#      determinism device gate and must never happen by accident;
#   3. the player is x64 (its PE machine type is read, not assumed);
#   4. the player's version resource agrees with ProjectSettings' bundleVersion,
#      so a stale build from before a version bump cannot ship under the new name;
#   5. the determinism self-check PASSES -- tools\windows-selfcheck.ps1 is run
#      here unless -ReuseSelfCheck points at a log that already passed and is
#      newer than the exe.
#
# Unity's *_DoNotShip / *_ButDontShipItWithYourGame folders are excluded: every
# build drops HeatonCA_BurstDebugInformation_DoNotShip beside the player (seen on
# macOS, Android and WebGL alike), and it is Burst's debug dump, not part of the app.
#
# Usage (on the Windows VM, from unity\heaton-ca):
#   powershell -ExecutionPolicy Bypass -File Packaging\windows\make-zip.ps1
#
#   -BuildDir <dir>            player to package (default build\windows-<arch>,
#                              which is where CIBuild.Windows writes)
#   -OutDir <dir>              where the zip lands (default build\dist)
#   -Arch x64|arm64            architecture asserted and stamped into the name
#                              (default x64; there is no CIBuild entry for arm64 yet)
#   -ReuseSelfCheck            accept an existing passing self-check log instead of
#                              running one, provided it is newer than the exe
#   -SelfCheckLog <file>       that log (default build\logs\win-selfcheck.log)
#   -SelfCheckTimeoutSec <n>   timeout handed to windows-selfcheck.ps1 (default 120)
#
# Output:
#   build\dist\HeatonCA-<version>-windows-<arch>.zip
#   build\dist\HeatonCA-<version>-windows-<arch>.zip.sha256
#
# The .sha256 is written in the `sha256sum` format (lowercase hex, two spaces,
# the zip's bare name, one LF, no BOM) so `shasum -a 256 -c` verifies it on the
# Mac, `sha256sum -c` on Linux, and `Get-FileHash` matches it (case-insensitively)
# on Windows.
#
# PUBLISH THAT HASH IN THE RELEASE NOTES: the exe is unsigned, first launch shows the
# SmartScreen "Windows protected your PC" prompt, and a hash people can check is
# the mitigation an Authenticode certificate would otherwise buy.
#
# The zip's single top-level folder is the release name, so extracting it never
# scatters 200 files into someone's Downloads.
[CmdletBinding()]
param(
    [string]$BuildDir,
    [string]$OutDir,
    [ValidateSet('x64', 'arm64')][string]$Arch = 'x64',
    [switch]$ReuseSelfCheck,
    [string]$SelfCheckLog,
    [int]$SelfCheckTimeoutSec = 120
)
$ErrorActionPreference = 'Stop'

# Packaging\windows\make-zip.ps1 -> the Unity project root is two levels up.
$Project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$Exe = 'HeatonCA.exe'

function Resolve-Under([string]$Path, [string]$Base)
{
    if (-not [System.IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $Base $Path }
    [System.IO.Path]::GetFullPath($Path)
}

$onWindows = $true
$isWindowsVar = Get-Variable -Name 'IsWindows' -ValueOnly -ErrorAction SilentlyContinue
if ($null -ne $isWindowsVar) { $onWindows = [bool]$isWindowsVar }
if (-not $onWindows) {
    throw 'make-zip.ps1 packages a Windows player and runs on Windows (see docs/windows-build.md)'
}
if ($SelfCheckTimeoutSec -lt 1 -or $SelfCheckTimeoutSec -gt 3600) {
    throw "-SelfCheckTimeoutSec must be between 1 and 3600 (got $SelfCheckTimeoutSec)"
}

if (-not $BuildDir) { $BuildDir = Join-Path $Project "build\windows-$Arch" }
if (-not $OutDir) { $OutDir = Join-Path $Project 'build\dist' }
if (-not $SelfCheckLog) { $SelfCheckLog = Join-Path $Project 'build\logs\win-selfcheck.log' }
$BuildDir = Resolve-Under $BuildDir $Project
$OutDir = Resolve-Under $OutDir $Project
$SelfCheckLog = Resolve-Under $SelfCheckLog $Project
if (-not (Test-Path -LiteralPath $BuildDir -PathType Container)) {
    throw "Unity player output not found: $BuildDir (build it with CIBuild.Windows first, or pass -BuildDir)"
}
$ExePath = Join-Path $BuildDir $Exe

# ---------------------------------------------------------------- the player

# PE machine type of an executable (IMAGE_FILE_MACHINE_AMD64 = 0x8664,
# ARM64 = 0xAA64): the COFF header sits at the offset stored at 0x3C, plus the
# four-byte "PE\0\0" signature.
function Get-PeMachine([string]$Path)
{
    $fs = [System.IO.File]::OpenRead($Path)
    try {
        $br = New-Object System.IO.BinaryReader $fs
        $fs.Seek(0x3C, 'Begin') | Out-Null
        $peOffset = $br.ReadInt32()
        $fs.Seek($peOffset + 4, 'Begin') | Out-Null
        $br.ReadUInt16()
    }
    finally { $fs.Dispose() }
}

$DataDir = [System.IO.Path]::GetFileNameWithoutExtension($Exe) + '_Data'
foreach ($rel in $Exe, 'UnityPlayer.dll', $DataDir, "$DataDir\globalgamemanagers") {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDir $rel))) {
        throw "Unity player incomplete: $BuildDir\$rel is missing (rebuild with CIBuild.Windows)"
    }
}
# The Mono tripwire, stated both ways. A Mono player carries the app's managed
# assemblies under <exe>_Data\Managed (confirmed against the macOS Mono player:
# Data/Managed/HeatonCAApp.dll, HeatonCA.Engine.dll, HeatonCA.SelfCheck.dll); an
# IL2CPP player has no such folder and grows GameAssembly.dll plus
# <exe>_Data\il2cpp_data instead. Between them those two facts identify the
# scripting backend without opening a single file.
$monoWhy = 'Standalone is Mono on purpose: switching Windows to IL2CPP is a scripting-backend ' +
           'change that re-arms the determinism device gate (docs/windows-build.md, ' +
           '"Standalone stays Mono").'
foreach ($rel in "$DataDir\Managed\HeatonCAApp.dll", "$DataDir\Managed\HeatonCA.Engine.dll") {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDir $rel))) {
        throw "$BuildDir does not look like a Mono player ($rel is missing). $monoWhy"
    }
}
foreach ($rel in 'GameAssembly.dll', "$DataDir\il2cpp_data") {
    if (Test-Path -LiteralPath (Join-Path $BuildDir $rel)) {
        throw "$BuildDir is an IL2CPP player ($rel is present). $monoWhy"
    }
}
$machine = Get-PeMachine $ExePath
$expectedMachine = @{ x64 = 0x8664; arm64 = 0xAA64 }[$Arch]
if ($machine -ne $expectedMachine) {
    throw ("{0} is not an {1} player (PE machine 0x{2:X4}) -- pass the matching -Arch/-BuildDir" -f $Exe, $Arch, $machine)
}

# --------------------------------------------------------------- the version

$verLine = Select-String -Path (Join-Path $Project 'ProjectSettings\ProjectSettings.asset') `
                         -Pattern '^\s*bundleVersion:\s*(\S+)' | Select-Object -First 1
if (-not $verLine) { throw 'bundleVersion not found in ProjectSettings/ProjectSettings.asset' }
$Version = $verLine.Matches[0].Groups[1].Value
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "bundleVersion '$Version' is not MAJOR.MINOR.PATCH; fix ProjectSettings before packaging"
}

# Unity stamps bundleVersion into the exe's Win32 version resource, so a player
# built before a version bump can be caught without launching it. Treat an
# unreadable stamp as a warning (a future Unity could stop writing it) and a
# stamp that disagrees as fatal -- shipping 2.0.0.zip full of 1.9 bits is the
# failure this check exists for.
$fileVersion = $null
try { $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath).ProductVersion } catch { }
if ([string]::IsNullOrWhiteSpace($fileVersion)) {
    Write-Warning "could not read a version resource from $Exe; skipping the bundleVersion cross-check"
}
else {
    # Unity writes four fields (2.0.0.0); compare the first three.
    $stamped = ($fileVersion.Trim() -split '[.+\-]')[0..2] -join '.'
    if ($stamped -ne $Version) {
        throw ("$Exe reports version $fileVersion but ProjectSettings says $Version -- " +
               'this player predates the version bump; rebuild with CIBuild.Windows')
    }
}

$Name = "HeatonCA-$Version-windows-$Arch"
Write-Output "Player:  $BuildDir"
Write-Output ("Version: {0} (exe resource {1})" -f $Version, $(if ($fileVersion) { $fileVersion } else { 'unreadable' }))
$commit = $null
try { $commit = (& git -C $Project rev-parse --short HEAD 2>$null) } catch { }
if ($commit) { Write-Output "Commit:  $commit" }

# ------------------------------------------------------------- the self-check

# The verdict must belong to THIS player, so a reused log has to be younger than
# the exe. Run in a child host process rather than dot-sourcing or & so the exit
# code is unambiguously the script's own; the child is whichever PowerShell is
# running this file, so pwsh 7 does not silently hand the work to 5.1.
if ($ReuseSelfCheck) {
    if (-not (Test-Path -LiteralPath $SelfCheckLog -PathType Leaf)) {
        throw "-ReuseSelfCheck: no self-check log at $SelfCheckLog (run tools\windows-selfcheck.ps1)"
    }
    $logTime = (Get-Item -LiteralPath $SelfCheckLog).LastWriteTimeUtc
    $exeTime = (Get-Item -LiteralPath $ExePath).LastWriteTimeUtc
    if ($logTime -lt $exeTime) {
        throw ("-ReuseSelfCheck: $SelfCheckLog is older than $Exe ($logTime < $exeTime), so it is a " +
               'verdict on a previous build; re-run tools\windows-selfcheck.ps1')
    }
    $logLines = @(Get-Content -LiteralPath $SelfCheckLog -Encoding UTF8)
    if (@($logLines | Where-Object { $_.Contains('SELF-CHECK FAIL') }).Count -gt 0) {
        throw "-ReuseSelfCheck: $SelfCheckLog says SELF-CHECK FAIL; this build must not ship"
    }
    if (@($logLines | Where-Object { $_.Contains('[HeatonCA] SELF-CHECK PASS') }).Count -eq 0) {
        throw "-ReuseSelfCheck: $SelfCheckLog holds no verdict; re-run tools\windows-selfcheck.ps1"
    }
    Write-Output "Self-check: reusing the PASS in $SelfCheckLog"
}
else {
    $selfCheckScript = Join-Path $Project 'tools\windows-selfcheck.ps1'
    if (-not (Test-Path -LiteralPath $selfCheckScript -PathType Leaf)) {
        throw "missing $selfCheckScript"
    }
    $psExe = 'powershell.exe'
    try { $psExe = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName } catch { }
    Write-Output 'Self-check: running tools\windows-selfcheck.ps1 ...'
    & $psExe -NoProfile -ExecutionPolicy Bypass -File $selfCheckScript `
        -BuildDir $BuildDir -LogFile $SelfCheckLog -TimeoutSec $SelfCheckTimeoutSec
    $rc = $LASTEXITCODE
    if ($rc -ne 0) {
        throw ("the determinism self-check did not pass (windows-selfcheck.ps1 exit $rc, log " +
               "$SelfCheckLog). Nothing is packaged: a player whose engine math disagrees with " +
               'the conformance vectors is not a release.')
    }
    Write-Output 'Self-check: PASS'
}

# ------------------------------------------------------------------- staging

$Stage = Join-Path $OutDir $Name
$Zip = Join-Path $OutDir "$Name.zip"
$Sha = "$Zip.sha256"

# A player still running from the stage holds its DLLs locked; the /MIR below
# would then die midway and leave a half-deleted folder. Fail whole instead.
$stagePrefix = $Stage.TrimEnd('\') + '\'   # with the separator, so a sibling folder cannot match
$live = Get-Process -Name ([System.IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($stagePrefix, [StringComparison]::OrdinalIgnoreCase) }
if ($live) {
    throw "the packaged app is running from $Stage (PID $($live.Id -join ', ')); close it before packaging"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
if (Test-Path -LiteralPath $Stage) { Remove-Item -Recurse -Force -LiteralPath $Stage }
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

# robocopy rather than Copy-Item: /MIR is exact and /XD takes the wildcards the
# Burst folder needs (it is named from productName, not from the exe). Its exit
# code is a bit field -- anything under 8 is success, 8 and up is a real error.
# stdout is captured so a good run stays quiet; stderr is deliberately NOT
# redirected, because 2>&1 on a native command under $ErrorActionPreference =
# 'Stop' turns its first stderr line into a terminating error before the exit
# code is ever read.
$roboLog = robocopy $BuildDir $Stage /MIR /NFL /NDL /NP /XD '*_DoNotShip' '*_ButDontShipItWithYourGame'
if ($LASTEXITCODE -ge 8) { $roboLog | Write-Output; throw "robocopy failed ($LASTEXITCODE)" }
$leaked = Get-ChildItem -LiteralPath $Stage -Recurse -Directory |
          Where-Object { $_.Name -like '*DoNotShip*' -or $_.Name -like '*DontShip*' }
if ($leaked) { throw "debug folder leaked into the stage: $($leaked.FullName -join ', ')" }

# The zip has no installer and no About-page equivalent of a license file, so
# the third-party notices ride along with the binary. Missing is a warning, not
# a failure: the notices belong to the repo, not to this channel.
$notices = Join-Path $Project 'THIRD_PARTY_NOTICES.md'
if (Test-Path -LiteralPath $notices -PathType Leaf) {
    Copy-Item -LiteralPath $notices -Destination (Join-Path $Stage 'THIRD_PARTY_NOTICES.md') -Force
}
else {
    Write-Warning "no THIRD_PARTY_NOTICES.md at $Project; the zip ships without it"
}

# --------------------------------------------------------------------- the zip

# ZipFile, not Compress-Archive: 5.1's cmdlet takes minutes over a player's few
# thousand files, and the $true argument puts everything under one top-level
# folder named for the release. Needs .NET Framework 4.6.1 or newer for '/'
# separators in the entry names (Windows 10 1607+ and every Windows 11 ship 4.8).
# (PowerShell 7 already has the type loaded and can refuse the Add-Type, so the
# load is best-effort and the type itself is what gets checked.)
try { Add-Type -AssemblyName System.IO.Compression.FileSystem } catch { }
if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    throw 'System.IO.Compression.ZipFile is unavailable; install .NET Framework 4.6.1 or newer'
}
if (Test-Path -LiteralPath $Zip) { Remove-Item -Force -LiteralPath $Zip }
if (Test-Path -LiteralPath $Sha) { Remove-Item -Force -LiteralPath $Sha }
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $Stage, $Zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)

$hash = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
$zipName = [System.IO.Path]::GetFileName($Zip)
# WriteAllText with ASCII and an explicit LF: Set-Content would add a BOM or a
# CRLF, and GNU sha256sum -c reads the CR as part of the file name and fails.
[System.IO.File]::WriteAllText($Sha, "$hash  $zipName`n", [System.Text.Encoding]::ASCII)

$sizeMb = [math]::Round((Get-Item -LiteralPath $Zip).Length / 1MB, 1)
Write-Output ''
Write-Output "OK: $Zip ($sizeMb MB)"
Write-Output "    $Sha"
Write-Output "SHA-256: $hash"
Write-Output ''
Write-Output 'Publish the hash with the download. Release-note lines to paste:'
Write-Output ''
Write-Output "    $zipName"
Write-Output "    SHA-256: $hash"
Write-Output ''
Write-Output '    Windows SmartScreen will warn on first run because the app is not'
Write-Output '    code-signed. Verify the download instead:'
Write-Output "        Get-FileHash -Algorithm SHA256 .\$zipName"
Write-Output '    Right-click the zip > Properties > Unblock before extracting.'
exit 0

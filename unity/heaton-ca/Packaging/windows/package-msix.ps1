#Requires -Version 5.1
# package-msix.ps1 -- turn the Unity Windows player into a Microsoft Store MSIX.
#
# The Windows sibling of Packaging\package-macos-appstore.sh, ported from
# heaton-life-unity's Packaging\package-windows-msix.ps1. Runs on Windows;
# makeappx.exe and makepri.exe come with the Windows 10/11 SDK (any Visual Studio
# install has them).
#
#   powershell -ExecutionPolicy Bypass -File Packaging\windows\package-msix.ps1
#       Pack build\windows-x64 -> build\msix\HeatonCA_<ver>_x64.msix. The upload needs
#       NO certificate: the Store re-signs the package on publication.
#
#   powershell -ExecutionPolicy Bypass -File Packaging\windows\package-msix.ps1 -Register
#       Instead of packing, loose-register the staged layout so the app installs into
#       its real MSIX container for local testing (package identity, registry
#       virtualization, the native menu bar, the Explorer reveal) with no signing at
#       all -- the Windows analog of the macOS script's sandbox-test mode: RUN THIS
#       FIRST. Requires Developer Mode (Settings > System > For developers).
#       Uninstall with:
#         Get-AppxPackage JeffHeaton.HeatonCA | Remove-AppxPackage
#
#   -BuildDir <dir>            player to package (default build\windows-<arch>, which is
#                              where CIBuild.Windows writes)
#   -Arch x64|arm64            architecture asserted and stamped into the manifest
#                              (default x64; there is no CIBuild entry for arm64 yet)
#   -Register                  loose-register instead of packing (see above)
#   -ReuseSelfCheck            accept an existing passing self-check log instead of
#                              running one, provided it is newer than the exe
#   -SelfCheckLog <file>       that log (default build\logs\win-selfcheck.log)
#   -SelfCheckTimeoutSec <n>   timeout handed to windows-selfcheck.ps1 (default 120)
#
# This is the Store's last gate before the bytes go public, so -- exactly like
# make-zip.ps1, whose checks it deliberately mirrors -- it refuses to package
# anything it cannot vouch for:
#
#   1. the player is COMPLETE, so a stage missing the data folder cannot pack
#      cleanly and fail only on the tester's machine;
#   2. the player is the MONO build the project settings promise, because UWP/IL2CPP
#      is a scripting-backend change that re-arms the determinism device gate
#      (README "Standalone stays Mono") -- the Desktop Bridge exists precisely so
#      Standalone does not have to move;
#   3. the player's PE machine type matches -Arch, read rather than assumed;
#   4. the player's serialized settings agree with ProjectSettings' bundleVersion and
#      productName (NOT the exe's version resource -- Unity 6000.5 stamps its own
#      version there, so that resource cannot see a stale app version at all);
#   5. the determinism self-check PASSES -- tools\windows-selfcheck.ps1 is run here
#      unless -ReuseSelfCheck points at a log that already passed and is newer than
#      the exe.
#
# Version = bundleVersion + ".0" (the Store requires the 4th field to be 0; name +
# version + arch must be unique across uploads, and a fix reaches installed users only
# as a HIGHER version). Unity's *_DoNotShip / *_ButDontShipItWithYourGame folders are
# excluded -- every HeatonCA build drops HeatonCA_BurstDebugInformation_DoNotShip
# beside the player.
#
# resources.pri: the manifest names Assets\StoreLogo.png etc., and the .scale-200.png
# siblings are only ever picked up through a Package Resource Index -- without one the
# shell draws the 44/50/150 px files at every display scale. The script therefore runs
# makepri over the logo set (msix\priconfig.xml + a generated layout.resfiles) after
# stamping, in both modes.
#
# Inside the container, HKCU is virtualized -- PlayerPrefs (AppSettings, Unity's
# Screenmanager keys) live in the package's private hive under
# %LOCALAPPDATA%\Packages\<PFN>\ and vanish on uninstall -- and so are AppData\Local
# and \Roaming, but LocalLow is NOT: Application.persistentDataPath (the gallery,
# snapshots, evolve finds, Player.log) stays the plain
# %USERPROFILE%\AppData\LocalLow\Jeff Heaton\HeatonCA, shared with an unpackaged build
# and untouched by Remove-AppxPackage.
[CmdletBinding()]
param(
    [string]$BuildDir,
    [ValidateSet('x64', 'arm64')][string]$Arch = 'x64',
    [switch]$Register,
    [switch]$ReuseSelfCheck,
    [string]$SelfCheckLog,
    [int]$SelfCheckTimeoutSec = 120
)
$ErrorActionPreference = 'Stop'

# Packaging\windows\package-msix.ps1 -> the Unity project root is two levels up.
$Project  = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$MsixDir  = Join-Path $PSScriptRoot 'msix'
$Manifest = Join-Path $MsixDir 'AppxManifest.xml'
$Exe      = 'HeatonCA.exe'
$DataDir  = [System.IO.Path]::GetFileNameWithoutExtension($Exe) + '_Data'

function Resolve-Under([string]$Path, [string]$Base)
{
    if (-not [System.IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $Base $Path }
    [System.IO.Path]::GetFullPath($Path)
}

$onWindows = $true
$isWindowsVar = Get-Variable -Name 'IsWindows' -ValueOnly -ErrorAction SilentlyContinue
if ($null -ne $isWindowsVar) { $onWindows = [bool]$isWindowsVar }
if (-not $onWindows) {
    throw 'package-msix.ps1 packages a Windows player and runs on Windows (see docs/windows-build.md)'
}
if ($SelfCheckTimeoutSec -lt 1 -or $SelfCheckTimeoutSec -gt 3600) {
    throw "-SelfCheckTimeoutSec must be between 1 and 3600 (got $SelfCheckTimeoutSec)"
}
if (-not (Test-Path -LiteralPath $Manifest -PathType Leaf)) { throw "missing $Manifest" }

if (-not $BuildDir) { $BuildDir = Join-Path $Project "build\windows-$Arch" }
if (-not $SelfCheckLog) { $SelfCheckLog = Join-Path $Project 'build\logs\win-selfcheck.log' }
$BuildDir = Resolve-Under $BuildDir $Project
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

foreach ($rel in $Exe, 'UnityPlayer.dll', $DataDir, "$DataDir\globalgamemanagers") {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDir $rel))) {
        throw "Unity player incomplete: $BuildDir\$rel is missing (rebuild with CIBuild.Windows)"
    }
}

# The Mono tripwire. A Mono player carries the app's managed assemblies under
# <exe>_Data\Managed; an IL2CPP player has GameAssembly.dll and no such tree.
$monoWhy = 'Standalone is Mono on purpose: UWP/IL2CPP is a scripting-backend change that re-arms ' +
           'the determinism device gate, which is why the Store package is a Desktop Bridge over ' +
           'the Mono player (README "Standalone stays Mono").'
foreach ($rel in "$DataDir\Managed\HeatonCAApp.dll", "$DataDir\Managed\HeatonCA.Engine.dll",
                 "$DataDir\Managed\HeatonCA.SelfCheck.dll") {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDir $rel))) {
        throw "$BuildDir does not look like a Mono player ($rel is missing). $monoWhy"
    }
}
if (Test-Path -LiteralPath (Join-Path $BuildDir 'GameAssembly.dll')) {
    throw "$BuildDir is an IL2CPP player (GameAssembly.dll is present). $monoWhy"
}

$machine = Get-PeMachine $ExePath
$expected = @{ x64 = 0x8664; arm64 = 0xAA64 }[$Arch]
if ($machine -ne $expected) {
    throw ("{0} is not an {1} player (PE machine 0x{2:X4}) -- pass the matching -Arch/-BuildDir" -f $Exe, $Arch, $machine)
}

# ------------------------------------------------------------------- version

$verLine = Select-String -Path (Join-Path $Project 'ProjectSettings\ProjectSettings.asset') `
                         -Pattern '^\s*bundleVersion:\s*(\S+)' | Select-Object -First 1
if (-not $verLine) { throw 'bundleVersion not found in ProjectSettings/ProjectSettings.asset' }
$AppVersion = $verLine.Matches[0].Groups[1].Value
if ($AppVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "bundleVersion '$AppVersion' is not MAJOR.MINOR.PATCH -- the Store version is bundleVersion + '.0'"
}
$Version = "$AppVersion.0"

# Catch a player built before a version bump, without launching it. NOT from the exe's
# Win32 version resource: Unity 6000.5 stamps ITS OWN version there (FileVersion
# 6000.5.0.8959100, ProductVersion "6000.5.0f1 (88b47c5e7076)") and leaves ProductName
# empty, so the resource says nothing about the app. The serialized player settings do:
# <exe>_Data\app.info carries companyName and productName, and globalgamemanagers
# carries bundleVersion.
#
# Substring, not parse: globalgamemanagers is a binary asset file whose serialization is
# not a public format. Measured on the 2.0.0 player -- it contains "2.0.0" and none of
# 1.9.0 / 2.0.1 / 3.0.0 / 1.0.0 / 0.0.0 -- so a stale player is caught. A version that
# is a substring of the stale one's (1.0 inside 1.0.1) would slip through; bundleVersion
# is MAJOR.MINOR.PATCH here, which makes that impossible.
$appInfoPath = Join-Path $BuildDir "$DataDir\app.info"
if (Test-Path -LiteralPath $appInfoPath) {
    $appInfo = @(Get-Content -LiteralPath $appInfoPath)
    $product = if ($appInfo.Count -ge 2) { $appInfo[1].Trim() } else { '' }
    if ($product -ne 'HeatonCA') {
        throw "app.info names product '$product', expected HeatonCA -- $BuildDir is not a HeatonCA player"
    }
}
else {
    Write-Warning "no $DataDir\app.info in the player; skipping the product-name check"
}
$ggmPath = Join-Path $BuildDir "$DataDir\globalgamemanagers"
$ggmText = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($ggmPath))
if (-not $ggmText.Contains($AppVersion)) {
    throw ("the player's settings do not carry version $AppVersion -- this player predates " +
           'the version bump in ProjectSettings; rebuild with CIBuild.Windows')
}

Write-Output "Player:  $BuildDir"
Write-Output "Version: $Version (bundleVersion $AppVersion, confirmed in the player settings)"
$commit = $null
try { $commit = (& git -C $Project rev-parse --short HEAD 2>$null) } catch { }
if ($commit) { Write-Output "Commit:  $commit" }

# ------------------------------------------------------------- the self-check

# The verdict must belong to THIS player, so a reused log has to be younger than the
# exe. Run in a child host process so the exit code is unambiguously the script's own.
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

# -------------------------------------------------------------------- staging

# Windows SDK tools: prefer the host's own architecture folder, fall back to any (an
# ARM64 box runs the x64 tools under emulation).
function Find-SdkTool([string]$Name)
{
    $hostArch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
    $all = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\*\$Name" -ErrorAction SilentlyContinue |
           Sort-Object FullName
    $tool = $all | Where-Object { $_.Directory.Name -eq $hostArch } | Select-Object -Last 1
    if (-not $tool) { $tool = $all | Select-Object -Last 1 }
    if (-not $tool) { throw "$Name not found -- install the Windows 10/11 SDK (ships with Visual Studio)" }
    $tool.FullName
}

# A registered app still running FROM the stage holds its DLLs locked, and the rebuild
# then dies midway leaving a half-deleted stage. Fail whole instead.
$OutRoot = Join-Path $Project 'build\msix'
$Stage   = Join-Path $OutRoot "stage-$Arch"
$stagePrefix = $Stage.TrimEnd('\') + '\'   # with the separator, so a sibling folder cannot match
$live = Get-Process -Name ([System.IO.Path]::GetFileNameWithoutExtension($Exe)) -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($stagePrefix, [StringComparison]::OrdinalIgnoreCase) }
if ($live) {
    throw "the registered app is running from $Stage (PID $($live.Id -join ', ')) -- close it before packaging"
}
if (Test-Path -LiteralPath $Stage) { Remove-Item -Recurse -Force -LiteralPath $Stage }
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

$roboLog = robocopy $BuildDir $Stage /MIR /NFL /NDL /NJH /NJS /XD '*_DoNotShip' '*_ButDontShipItWithYourGame'
if ($LASTEXITCODE -ge 8) { $roboLog | Write-Output; throw "robocopy failed ($LASTEXITCODE)" }
$leaked = Get-ChildItem $Stage -Recurse -Directory |
          Where-Object { $_.Name -like '*DoNotShip*' -or $_.Name -like '*DontShip*' }
if ($leaked) { throw "debug folder leaked into the stage: $($leaked.FullName -join ', ')" }

(Get-Content -LiteralPath $Manifest -Raw) -replace '__VERSION__', $Version -replace '__ARCH__', $Arch |
    Set-Content -Encoding UTF8 (Join-Path $Stage 'AppxManifest.xml')
Copy-Item -Recurse -Force (Join-Path $MsixDir 'Assets') (Join-Path $Stage 'Assets')

# Every logo slot the manifest names, at both scales -- plus the unplated targetsize
# set, without which the taskbar draws the 44 px logo on an accent-colored plate.
foreach ($logo in 'Square44x44Logo', 'Square150x150Logo', 'Wide310x150Logo', 'StoreLogo') {
    foreach ($suffix in '', '.scale-200') {
        $png = Join-Path $Stage "Assets\$logo$suffix.png"
        if (-not (Test-Path -LiteralPath $png)) {
            throw "logo missing: $png (run Packaging\windows\msix\make-logos.ps1)"
        }
    }
}
foreach ($size in 16, 24, 32, 48, 256) {
    $png = Join-Path $Stage "Assets\Square44x44Logo.targetsize-${size}_altform-unplated.png"
    if (-not (Test-Path -LiteralPath $png)) {
        throw "unplated taskbar icon missing: $png (run Packaging\windows\msix\make-logos.ps1)"
    }
}

[xml]$stampedXml = Get-Content -LiteralPath (Join-Path $Stage 'AppxManifest.xml') -Raw
$identity = $stampedXml.Package.Identity
if ($stampedXml.Package.Applications.Application.Executable -ne $Exe) {
    throw "manifest Executable is '$($stampedXml.Package.Applications.Application.Executable)', expected $Exe"
}
Write-Output "Package: $($identity.Name) $($identity.Version) ($($identity.ProcessorArchitecture)), publisher $($identity.Publisher)"

# Package Resource Index over the logo set (see header). The resfiles indexer takes no
# wildcards, so the list is generated from what was staged; it is removed again so it
# does not ship. /mn reads the STAMPED manifest for the resource map name.
$resFiles = Join-Path $Stage 'layout.resfiles'
Get-ChildItem (Join-Path $Stage 'Assets') -Filter *.png | ForEach-Object { "Assets\$($_.Name)" } |
    Set-Content -Encoding ASCII $resFiles
$makePri = Find-SdkTool 'makepri.exe'
# stdout is captured so a good run stays quiet; stderr is NOT redirected -- under
# $ErrorActionPreference = 'Stop', 2>&1 on a native tool turns its first stderr line
# into a terminating NativeCommandError before the exit code is ever checked.
$priLog = & $makePri new /pr $Stage /cf (Join-Path $MsixDir 'priconfig.xml') /of (Join-Path $Stage 'resources.pri') `
                     /mn (Join-Path $Stage 'AppxManifest.xml') /o
if ($LASTEXITCODE -ne 0) { $priLog | Write-Output; throw "makepri failed ($LASTEXITCODE)" }
Remove-Item -LiteralPath $resFiles
if (-not (Test-Path -LiteralPath (Join-Path $Stage 'resources.pri'))) { throw 'makepri produced no resources.pri' }

# ------------------------------------------------------------------- register

if ($Register) {
    # A development-mode registration cannot be replaced at the same version once its
    # manifest differs (0x80073CFB: "increment the version number ... or remove the old
    # package"), so drop any existing registration first. That discards only the
    # package-private hive (PlayerPrefs); the LocalLow data is not touched.
    $existing = Get-AppxPackage -Name $identity.Name
    if ($existing) {
        Write-Output "Removing existing registration $($existing.PackageFullName)"
        $existing | Remove-AppxPackage
    }
    Add-AppxPackage -Register (Join-Path $Stage 'AppxManifest.xml')
    $pkg = Get-AppxPackage -Name $identity.Name | Select-Object -First 1
    Write-Output "OK: registered $Version ($Arch) from $Stage"
    Write-Output "Launch 'HeatonCA' from Start, or:"
    Write-Output "  explorer.exe shell:AppsFolder\$($pkg.PackageFamilyName)!HeatonCA"
    Write-Output 'The gallery, snapshots, evolve finds and Player.log are NOT virtualized -- they share'
    Write-Output "  $env:USERPROFILE\AppData\LocalLow\Jeff Heaton\HeatonCA"
    Write-Output 'with an unpackaged build and survive uninstall; only PlayerPrefs (HKCU) are'
    Write-Output "package-private, under $env:LOCALAPPDATA\Packages\$($pkg.PackageFamilyName)\"
    return
}

# ----------------------------------------------------------------------- pack

$makeAppx = Find-SdkTool 'makeappx.exe'
$Msix = Join-Path $OutRoot "HeatonCA_${Version}_$Arch.msix"
if (Test-Path -LiteralPath $Msix) { Remove-Item -Force -LiteralPath $Msix }
$packLog = & $makeAppx pack /d $Stage /p $Msix /o
if ($LASTEXITCODE -ne 0) { $packLog | Write-Output; throw "makeappx failed ($LASTEXITCODE)" }

Write-Output "OK: $Msix ($([math]::Round((Get-Item $Msix).Length / 1MB, 1)) MB)"
Write-Output 'Upload: Partner Center > HeatonCA > Start submission > Packages (unsigned is'
Write-Output 'correct -- the Store signs it). The package identity above must match the product'
Write-Output 'identity page there. For a local install test, re-run with -Register.'

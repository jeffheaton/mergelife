# Renders the MSIX package logos in Packaging/msix/Assets/ from the app icon master
# (the Windows sibling of Assets/Editor/SetIOSIcons.cs's Pillow recipe). Re-run only
# when Assets/Icons/heatonca_icon_1024.png changes; the PNGs are committed.
#
#   powershell -ExecutionPolicy Bypass -File Packaging/msix/make-logos.ps1
#
# Ported from heaton-life-unity's Packaging/msix/make-logos.ps1, whose recipe is matched
# pixel-for-pixel to a Store-accepted logo set: the WHOLE 1024 canvas - the rounded tile
# plus its transparent margin - is scaled onto a transparent canvas of each slot's size,
# so the tile floats with its own margin and rounded corners, the way macOS/Windows
# desktop icons do. No cropping and no corner flood: unlike iOS, Windows keeps the alpha
# channel, and the Start menu / taskbar draw the icon over arbitrary backgrounds. The
# wide tile centers the canvas at 82% of the tile height.
#
# Slots (manifest name -> scale-100 / scale-200 size): Square44x44Logo 44/88 (taskbar,
# Start list), Square150x150Logo 150/300 (medium tile, Settings > Apps),
# Wide310x150Logo 310x150/620x300 (wide tile), StoreLogo 50/100 (Package/Properties/Logo).
#
# System.Drawing scales in PREMULTIPLIED alpha on purpose: the master's transparent
# pixels are rgba(0,0,0,0), and bicubic filtering in straight alpha would bleed that
# black into the tile's anti-aliased edge as a dark halo.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$Repo   = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$Master = Join-Path $Repo 'Assets\Icons\heatonca_icon_1024.png'
$OutDir = Join-Path $PSScriptRoot 'Assets'
if (-not (Test-Path $Master)) { throw "icon master not found: $Master" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Source = [System.Drawing.Bitmap]::FromFile($Master)
if ($Source.Width -ne 1024 -or $Source.Height -ne 1024) {
    throw "icon master must be 1024x1024, got $($Source.Width)x$($Source.Height)"
}
# Premultiplied working copy (see header).
$Premul = New-Object System.Drawing.Bitmap 1024, 1024, ([System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
$g = [System.Drawing.Graphics]::FromImage($Premul)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawImage($Source, 0, 0, 1024, 1024)
$g.Dispose()

function Render-Logo([string]$Name, [int]$Width, [int]$Height, [int]$IconSize) {
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingMode   = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $x = [int][Math]::Floor(($Width - $IconSize) / 2)
    $y = [int][Math]::Floor(($Height - $IconSize) / 2)
    # ImageAttributes with WrapMode TileFlipXY stops bicubic sampling from pulling the
    # (transparent) out-of-bounds border into the outermost destination pixels.
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $dest = New-Object System.Drawing.Rectangle $x, $y, $IconSize, $IconSize
    $g.DrawImage($Premul, $dest, 0, 0, 1024, 1024, [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $g.Dispose()
    $path = Join-Path $OutDir "$Name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("{0,-34} {1}x{2}  (icon {3} px)" -f "$Name.png", $Width, $Height, $IconSize)
}

foreach ($scale in 1, 2) {
    $suffix = if ($scale -eq 1) { '' } else { '.scale-200' }
    Render-Logo "Square44x44Logo$suffix"   (44 * $scale)  (44 * $scale)  (44 * $scale)
    Render-Logo "Square150x150Logo$suffix" (150 * $scale) (150 * $scale) (150 * $scale)
    Render-Logo "StoreLogo$suffix"         (50 * $scale)  (50 * $scale)  (50 * $scale)
    Render-Logo "Wide310x150Logo$suffix"   (310 * $scale) (150 * $scale) ([int][Math]::Round(150 * $scale * 0.82))
}

# Taskbar/Start-list icons: without targetsize variants carrying altform-unplated,
# Windows draws Square44x44Logo on a PLATE of the manifest's BackgroundColor -
# "transparent" means the user's ACCENT color, i.e. a colored box behind the tile. The
# unplated forms are what the Windows 11 taskbar prefers; lightunplated serves the
# light-theme taskbar; the plain targetsize files back both. All are found through
# resources.pri (package-windows-msix.ps1 indexes every staged Assets\*.png), and the
# same full-canvas art works for all three forms.
foreach ($size in 16, 24, 32, 48, 256) {
    foreach ($form in '', '_altform-unplated', '_altform-lightunplated') {
        Render-Logo "Square44x44Logo.targetsize-$size$form" $size $size $size
    }
}

$Premul.Dispose()
$Source.Dispose()
Write-Host "OK: $OutDir"

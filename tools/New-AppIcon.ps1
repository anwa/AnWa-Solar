<#
.SYNOPSIS
  Generates the application icon src/AnWaSolar.App/Resources/AppIcon.ico.

.DESCRIPTION
  Draws the AnWa-Solar icon in the colours of the application theme: a rounded square with an
  indigo gradient (#5C6BC0 to #1A237E), an amber sun (#FFC107) and a PV module with a white cell
  grid. Small sizes leave out the sun rays and use fewer cells so the shape stays clear. All sizes
  from 16 to 256 px are written as PNG-compressed entries into one ICO file; -PreviewPath also
  writes a preview strip.

    pwsh tools/New-AppIcon.ps1
    pwsh tools/New-AppIcon.ps1 -PreviewPath preview.png
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\AnWaSolar.App\Resources\AppIcon.ico'),
    [string]$PreviewPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Color([string]$hex, [int]$alpha = 255) { [System.Drawing.Color]::FromArgb($alpha, [System.Drawing.ColorTranslator]::FromHtml($hex)) }

$gradientTop = Color '#5C6BC0'
$gradientMid = Color '#3F51B5'
$gradientBottom = Color '#1A237E'
$sun = Color '#FFC107'
$panel = Color '#0D1240'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = [Math]::Max(0.5, 2 * $r)
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Pen([System.Drawing.Color]$color, [float]$width) {
    $pen = [System.Drawing.Pen]::new($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function New-IconBitmap([int]$size) {
    $s = [float]$size
    $small = $size -le 24
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        # Background: rounded square with a diagonal indigo gradient.
        $background = New-RoundedRect 0 0 ($s - 0.5) ($s - 0.5) ($s * 0.22)
        $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.PointF]::new(0, 0), [System.Drawing.PointF]::new($s, $s), $gradientTop, $gradientBottom)
        $blend = [System.Drawing.Drawing2D.ColorBlend]::new(3)
        $blend.Colors = @($gradientTop, $gradientMid, $gradientBottom)
        $blend.Positions = @([float]0, [float]0.5, [float]1)
        $brush.InterpolationColors = $blend
        $g.FillPath($brush, $background)

        # Sun in the upper right, with rays from 32 px on.
        $cx = $s * 0.66; $cy = $s * 0.31
        $radius = $s * ($small ? 0.2 : 0.15)
        if (-not $small) {
            $ray = New-Pen $sun ([float]($s * 0.045))
            for ($i = 0; $i -lt 8; $i++) {
                $angle = [Math]::PI / 4 * $i
                $inner = $radius * 1.4; $outer = $radius * 1.85
                $g.DrawLine($ray, $cx + $inner * [Math]::Cos($angle), $cy + $inner * [Math]::Sin($angle), $cx + $outer * [Math]::Cos($angle), $cy + $outer * [Math]::Sin($angle))
            }
        }
        $g.FillEllipse([System.Drawing.SolidBrush]::new($sun), $cx - $radius, $cy - $radius, 2 * $radius, 2 * $radius)

        # PV module in the lower half: dark panel, white frame and cell grid.
        $px = $s * 0.13; $py = $s * 0.50; $pw = $s * 0.74; $ph = $s * 0.36
        $frame = New-RoundedRect $px $py $pw $ph ($s * 0.04)
        $g.FillPath([System.Drawing.SolidBrush]::new($panel), $frame)
        $g.DrawPath((New-Pen ([System.Drawing.Color]::White) ([float][Math]::Max(1.2, $s * 0.045))), $frame)
        $columns = $small ? 2 : 4
        $rows = 2
        $grid = New-Pen (Color '#FFFFFF' 200) ([float][Math]::Max(1.0, $s * 0.025))
        for ($c = 1; $c -lt $columns; $c++) {
            $x = $px + $pw * $c / $columns
            $g.DrawLine($grid, $x, $py, $x, $py + $ph)
        }
        for ($r = 1; $r -lt $rows; $r++) {
            $y = $py + $ph * $r / $rows
            $g.DrawLine($grid, $px, $y, $px + $pw, $y)
        }
    }
    finally {
        $g.Dispose()
    }

    return $bitmap
}

$bitmaps = foreach ($size in $sizes) { New-IconBitmap $size }
$images = foreach ($bitmap in $bitmaps) {
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    , $stream.ToArray()
}

$out = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($out)
$writer.Write([UInt16]0)               # reserved
$writer.Write([UInt16]1)               # type: icon
$writer.Write([UInt16]$sizes.Count)    # image count
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)             # palette
    $writer.Write([byte]0)             # reserved
    $writer.Write([UInt16]1)           # colour planes
    $writer.Write([UInt16]32)          # bits per pixel
    $writer.Write([UInt32]$images[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $writer.Write($img) }
$writer.Flush()

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($fullPath)) | Out-Null
[System.IO.File]::WriteAllBytes($fullPath, $out.ToArray())
Write-Host "Icon written: $fullPath"

if ($PreviewPath) {
    # All sizes side by side on a light and a dark strip, as they appear in Explorer and the taskbar.
    $width = [int](($sizes | Measure-Object -Sum).Sum + 20 * ($sizes.Count + 1))
    $preview = [System.Drawing.Bitmap]::new($width, 2 * 296)
    $g = [System.Drawing.Graphics]::FromImage($preview)
    $g.Clear([System.Drawing.Color]::White)
    $g.FillRectangle([System.Drawing.SolidBrush]::new((Color '#202020')), 0, 296, $width, 296)
    foreach ($row in 0, 1) {
        $x = 20
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $g.DrawImage($bitmaps[$i], $x, $row * 296 + 20 + (256 - $sizes[$i]), $sizes[$i], $sizes[$i])
            $x += $sizes[$i] + 20
        }
    }
    $g.Dispose()
    $preview.Save([System.IO.Path]::GetFullPath($PreviewPath), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Preview written: $PreviewPath"
}

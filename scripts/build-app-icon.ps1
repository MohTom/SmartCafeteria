# Generates the application icon set (AppIcon.ico + AppIcon.png) from the
# glossy cafe artwork. Trims transparent margins, pads 4%, centres on a square
# canvas, then emits multi-resolution ICO frames (16-128 DIB, 256 PNG).
param(
    [string]$SourcePng = '',
    [string]$OutPng = (Join-Path $PSScriptRoot '..\src\SmartCafeteria\Resources\AppIcon.png'),
    [string]$OutIco = (Join-Path $PSScriptRoot '..\src\SmartCafeteria\Resources\AppIcon.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($SourcePng)) {
    $SourcePng = (Get-Item (Join-Path $PSScriptRoot '..\Resources\Glossy*Icon Set.png')).FullName
}

function Get-AlphaBounds([System.Drawing.Bitmap]$bmp) {
    $minX = $bmp.Width; $minY = $bmp.Height; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            if ($bmp.GetPixel($x, $y).A -gt 8) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { throw 'Source image is fully transparent.' }
    return [System.Drawing.Rectangle]::FromLTRB($minX, $minY, $maxX + 1, $maxY + 1)
}

function New-ResizedBitmap([System.Drawing.Bitmap]$src, [int]$size) {
    $dst = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $rect = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
        $g.DrawImage($src, $rect, [System.Drawing.Rectangle]::new(0, 0, $src.Width, $src.Height), [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally { $g.Dispose() }
    return $dst
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = [System.IO.MemoryStream]::new()
    try {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return $ms.ToArray()
    }
    finally { $ms.Dispose() }
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $maskStride = [int]([math]::Floor(($w + 31) / 32) * 4)
    $ms = [System.IO.MemoryStream]::new()
    try {
        $bw = [System.IO.BinaryWriter]::new($ms)
        try {
            $bw.Write([int]40); $bw.Write([int]$w); $bw.Write([int]($h * 2))
            $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0)
            $bw.Write([int]($w * $h * 4)); $bw.Write([int]0); $bw.Write([int]0)
            $bw.Write([int]0); $bw.Write([int]0)
            for ($y = $h - 1; $y -ge 0; $y--) {
                for ($x = 0; $x -lt $w; $x++) {
                    $c = $bmp.GetPixel($x, $y)
                    $bw.Write([byte]$c.B); $bw.Write([byte]$c.G)
                    $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
                }
            }
            $bw.Write([byte[]]::new($maskStride * $h))
        }
        finally { $bw.Dispose() }
        return $ms.ToArray()
    }
    finally { $ms.Dispose() }
}

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $SourcePng))
try {
    Write-Host "source $($source.Width)x$($source.Height) $($source.PixelFormat)"
    $bounds = Get-AlphaBounds $source
    Write-Host "alpha bbox $($bounds.X),$($bounds.Y) $($bounds.Width)x$($bounds.Height)"

    $side = [math]::Max($bounds.Width, $bounds.Height)
    $pad = [int][math]::Round($side * 0.04)
    $canvasSide = $side + 2 * $pad

    $square = [System.Drawing.Bitmap]::new($canvasSide, $canvasSide, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $g = [System.Drawing.Graphics]::FromImage($square)
        try {
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $dx = $pad + [int](($side - $bounds.Width) / 2)
            $dy = $pad + [int](($side - $bounds.Height) / 2)
            $g.DrawImage($source, [System.Drawing.Rectangle]::new($dx, $dy, $bounds.Width, $bounds.Height), $bounds, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally { $g.Dispose() }

        $big = New-ResizedBitmap $square 512
        try { $big.Save((Join-Path (Split-Path -Parent $OutPng) 'AppIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
        finally { $big.Dispose() }
        Write-Host "wrote $OutPng"

        $sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
        $frames = @()
        foreach ($size in $sizes) {
            $bmp = New-ResizedBitmap $square $size
            try {
                $data = if ($size -ge 256) { Get-PngBytes $bmp } else { Get-DibBytes $bmp }
            }
            finally { $bmp.Dispose() }
            $frames += , @($size, $data)
            Write-Host "frame $size`: $($data.Length) bytes"
        }

        $fs = [System.IO.FileStream]::new($OutIco, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        try {
            $bw = [System.IO.BinaryWriter]::new($fs)
            try {
                $bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$frames.Count)
                $offset = 6 + 16 * $frames.Count
                foreach ($frame in $frames) {
                    $size = $frame[0]; $data = $frame[1]
                    $dim = if ($size -ge 256) { 0 } else { $size }
                    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
                    $bw.Write([byte]0); $bw.Write([byte]0)
                    $bw.Write([int16]1); $bw.Write([int16]32)
                    $bw.Write([int]$data.Length); $bw.Write([int]$offset)
                    $offset += $data.Length
                }
                foreach ($frame in $frames) { $bw.Write([byte[]]$frame[1]) }
            }
            finally { $bw.Dispose() }
        }
        finally { $fs.Dispose() }
        Write-Host "wrote $OutIco ($((Get-Item -LiteralPath $OutIco).Length) bytes)"
    }
    finally { $square.Dispose() }
}
finally { $source.Dispose() }

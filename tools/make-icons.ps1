# Generates the Gamer icon (PNG sizes + multi-size ICO). Geometry mirrors Gamer.Ui/Components/Logo.razor (256x256 design grid).
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\assets')
)

Add-Type -AssemblyName System.Drawing

function New-ControllerPath([single]$s) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    function Pt($x, $y) { New-Object System.Drawing.PointF(($x * $s), ($y * $s)) }
    $p.StartFigure()
    $p.AddLine((Pt 70 92), (Pt 186 92))
    $p.AddBezier((Pt 186 92), (Pt 210 92), (Pt 224 112), (Pt 230 144))
    $p.AddLine((Pt 230 144), (Pt 236 178))
    $p.AddBezier((Pt 236 178), (Pt 239 195), (Pt 230 204), (Pt 217 204))
    $p.AddBezier((Pt 217 204), (Pt 208 204), (Pt 202 199), (Pt 195 190))
    $p.AddLine((Pt 195 190), (Pt 186 178))
    $p.AddLine((Pt 186 178), (Pt 70 178))
    $p.AddLine((Pt 70 178), (Pt 61 190))
    $p.AddBezier((Pt 61 190), (Pt 54 199), (Pt 48 204), (Pt 39 204))
    $p.AddBezier((Pt 39 204), (Pt 26 204), (Pt 17 195), (Pt 20 178))
    $p.AddLine((Pt 20 178), (Pt 26 144))
    $p.AddBezier((Pt 26 144), (Pt 32 112), (Pt 46 92), (Pt 70 92))
    $p.CloseFigure()
    return $p
}

function New-RoundedRectPath([single]$size, [single]$radius) {
    $d = $radius * 2
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $d, $d, 180, 90)
    $p.AddArc($size - $d, 0, $d, $d, 270, 90)
    $p.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $p.AddArc(0, $size - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $s = $size / 256.0
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.ColorTranslator]::FromHtml('#7c5cff'), [System.Drawing.ColorTranslator]::FromHtml('#14d9c0'), 45.0)
    $g.FillPath($gradient, (New-RoundedRectPath $size (58 * $s)))

    $g.FillPath([System.Drawing.Brushes]::White, (New-ControllerPath $s))

    $dark = [System.Drawing.ColorTranslator]::FromHtml('#2b1f7a')
    $pen = New-Object System.Drawing.Pen($dark, [single](13 * $s))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLine($pen, [single](92 * $s), [single](114 * $s), [single](92 * $s), [single](148 * $s))
    $g.DrawLine($pen, [single](75 * $s), [single](131 * $s), [single](109 * $s), [single](131 * $s))
    $brush = New-Object System.Drawing.SolidBrush($dark)
    foreach ($c in @(@(166, 124), @(188, 142))) {
        $r = 9 * $s
        $g.FillEllipse($brush, [single]($c[0] * $s - $r), [single]($c[1] * $s - $r), [single](2 * $r), [single](2 * $r))
    }

    $g.Dispose()
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $stream.ToArray()
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

foreach ($size in 192, 512) {
    $bitmap = New-IconBitmap $size
    $bitmap.Save((Join-Path $OutputDirectory "icon-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

$icoSizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $icoSizes) {
    $bitmap = New-IconBitmap $size
    [pscustomobject]@{ Size = $size; Bytes = (Get-PngBytes $bitmap) }
    $bitmap.Dispose()
}

$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
[System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'gamer.ico'), $out.ToArray())

Write-Host "Icons written to $OutputDirectory"

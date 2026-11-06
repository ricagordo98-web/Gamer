# Renders assets\gamer-icon.svg into the PNG sizes (PWA) and the multi-size gamer.ico.
# Uses the Chromium headless shell that Playwright installs (see Gamer.bat / README), so no extra tools are needed.
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\assets'),
    [string]$Svg = (Join-Path $PSScriptRoot '..\assets\gamer-icon.svg')
)

$ErrorActionPreference = 'Stop'
$chrome = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'ms-playwright') -Recurse -Filter 'chrome-headless-shell.exe' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $chrome) { throw 'chrome-headless-shell.exe not found. Run: playwright.ps1 install chromium' }

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$svgUri = ([System.Uri](Resolve-Path $Svg).Path).AbsoluteUri
$temp = Join-Path ([System.IO.Path]::GetTempPath()) 'gamer-icons'
New-Item -ItemType Directory -Force $OutputDirectory, $temp | Out-Null

function New-Png([int]$size, [string]$path) {
    $html = Join-Path $temp "render-$size.html"
    Set-Content -Path $html -Encoding UTF8 -Value "<html><body style='margin:0;background:transparent'><img src='$svgUri' width='$size' height='$size' style='display:block'></body></html>"
    & $chrome.FullName --headless --disable-gpu --hide-scrollbars --default-background-color=00000000 `
        "--window-size=$size,$size" "--screenshot=$path" ([System.Uri]$html).AbsoluteUri | Out-Null
    if (-not (Test-Path $path)) { throw "Failed to render $size px icon." }
}

foreach ($size in 192, 512) { New-Png $size (Join-Path $OutputDirectory "icon-$size.png") }

$images = foreach ($size in 16, 24, 32, 48, 64, 128, 256) {
    $png = Join-Path $temp "ico-$size.png"
    New-Png $size $png
    [pscustomobject]@{ Size = $size; Bytes = [System.IO.File]::ReadAllBytes($png) }
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

Copy-Item (Join-Path $OutputDirectory 'icon-192.png'), (Join-Path $OutputDirectory 'icon-512.png') (Join-Path $PSScriptRoot '..\src\Gamer.Web\wwwroot') -Force
Remove-Item $temp -Recurse -Force
Write-Host "Icons written to $OutputDirectory"

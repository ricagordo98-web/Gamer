# Builds Gamer in Release into ..\Release (API + web client + Windows app), copies the launcher and
# creates a desktop shortcut. The API database (Release\Api\data) is preserved between runs.
param(
    [string]$ReleaseDirectory = (Join-Path $PSScriptRoot '..\Release'),
    [switch]$SkipShortcut
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$release = [System.IO.Path]::GetFullPath($ReleaseDirectory)
$apiOut = Join-Path $release 'Api'
$appOut = Join-Path $release 'App'
$webTemp = Join-Path ([System.IO.Path]::GetTempPath()) 'gamer-web-publish'

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed." }
}

Get-Process -Name Gamer, Gamer.Api -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-Item $webTemp, $appOut, (Join-Path $apiOut 'wwwroot') -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $release, $apiOut, $appOut | Out-Null

Write-Host '==> Publishing web client'
Invoke-Dotnet publish (Join-Path $root 'src\Gamer.Web') -c Release -o $webTemp --nologo -v q

Write-Host '==> Publishing API'
Invoke-Dotnet publish (Join-Path $root 'src\Gamer.Api') -c Release -o $apiOut --nologo -v q
Copy-Item (Join-Path $webTemp 'wwwroot') (Join-Path $apiOut 'wwwroot') -Recurse

Write-Host '==> Building Windows app'
Invoke-Dotnet build (Join-Path $root 'src\Gamer.Maui') -f net10.0-windows10.0.19041.0 -c Release --nologo -v q
$appBuild = Get-ChildItem (Join-Path $root 'src\Gamer.Maui\bin\Release') -Recurse -Filter 'Gamer.exe' | Select-Object -First 1
Copy-Item (Join-Path $appBuild.DirectoryName '*') $appOut -Recurse -Force

Copy-Item (Join-Path $PSScriptRoot 'Gamer.bat') $release -Force
Copy-Item (Join-Path $root 'assets\gamer.ico') $release -Force

if (-not $SkipShortcut) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $desktop 'Gamer.lnk'))
    $shortcut.TargetPath = Join-Path $release 'Gamer.bat'
    $shortcut.WorkingDirectory = $release
    $shortcut.IconLocation = Join-Path $release 'gamer.ico'
    $shortcut.WindowStyle = 7
    $shortcut.Description = 'Gamer - game backlog'
    $shortcut.Save()
    Write-Host "==> Shortcut created on $desktop"
}

Write-Host "==> Release ready: $release"

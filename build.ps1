# 编译并输出到 dist/
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot 'src')
dotnet build -c Release
$exe = Join-Path (Get-Location) 'bin\Release\net48\Polaris修复插件.exe'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $exe $dist -Force
Write-Host "输出: $dist\Polaris修复插件.exe"
Pop-Location

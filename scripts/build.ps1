[CmdletBinding()]
param(
    [string]$Source = "src/openclawtp.cs",
    [string]$Output = "dist/openclawtp.exe",
    [string]$IconPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$csc = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path $csc)) {
    throw "未找到 C# 编译器: $csc"
}

$sourcePath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Source))
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
$outputDir = Split-Path -Parent $outputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$args = @(
    '/nologo'
    '/target:winexe'
    "/out:$outputPath"
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Drawing.dll'
)

if ($IconPath) {
    $resolvedIcon = [IO.Path]::GetFullPath((Join-Path $repoRoot $IconPath))
    if (Test-Path $resolvedIcon) {
        $args += "/win32icon:$resolvedIcon"
    }
}

$args += $sourcePath
& $csc @args
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码: $LASTEXITCODE"
}

$exampleConfig = Join-Path $repoRoot 'config/openclawtp.runtime.example.json'
if (Test-Path $exampleConfig) {
    Copy-Item $exampleConfig (Join-Path $outputDir 'openclawtp.runtime.example.json') -Force
}

Write-Host "Build complete: $outputPath"
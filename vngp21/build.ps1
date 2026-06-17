# Build vngp21 bằng MSBuild Full (.NET Framework) — không dùng "dotnet build".
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Platform = 'AnyCPU'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'vngp21.csproj'

function Get-VsMsBuildPath {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "Không tìm thấy vswhere. Cài Visual Studio 2019/2022 (workload .NET desktop + MSBuild)."
    }
    $install = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $install) {
        throw "Không tìm thấy Visual Studio có MSBuild."
    }
    $msbuild = Join-Path $install 'MSBuild\Current\Bin\MSBuild.exe'
    if (-not (Test-Path $msbuild)) {
        throw "Không tìm thấy MSBuild.exe tại: $msbuild"
    }
    return $msbuild
}

$msbuild = Get-VsMsBuildPath
Write-Host "MSBuild: $msbuild" -ForegroundColor Cyan

Write-Host "Build vngp21 ($Configuration|$Platform)..." -ForegroundColor Cyan
& $msbuild $projectFile /restore /p:Configuration=$Configuration /p:Platform=$Platform /v:m
exit $LASTEXITCODE

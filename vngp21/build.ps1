# Build vngp21 bằng MSBuild Full (.NET Framework) — không dùng "dotnet build".
# Smith.WPF.HtmlEditor + Interop.MSHTML: COM 32-bit (win-x86); vngp21 PlatformTarget=x86.
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Platform = 'AnyCPU',
    [switch]$SkipSmithHtmlEditor
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'vngp21.csproj'
$smithProject = Join-Path (Split-Path $projectRoot -Parent) 'SmithHtmlEditor\SmithHtmlEditor.csproj'

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

if (-not $SkipSmithHtmlEditor -and (Test-Path $smithProject)) {
    Write-Host "Build SmithHtmlEditor (win-x86 / x86)..." -ForegroundColor Cyan
    & $msbuild $smithProject /restore /p:Configuration=$Configuration /p:RuntimeIdentifier=win-x86 /p:PlatformTarget=x86 /v:m
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Build vngp21 ($Configuration|$Platform)..." -ForegroundColor Cyan
& $msbuild $projectFile /restore /p:Configuration=$Configuration /p:Platform=$Platform /v:m
exit $LASTEXITCODE

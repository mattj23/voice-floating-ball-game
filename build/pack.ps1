<#
.SYNOPSIS
    Publishes the game and packages it with Velopack.

.DESCRIPTION
    Runs on Windows. Creates a self-contained publish for the requested runtime and packages it:
      win-x64    Setup.exe installer, portable zip, and full nupkg
      linux-x64  AppImage, cross-packed from Windows

    macOS cannot be packed from Windows; use build/pack.sh on a Mac.

    Output goes to artifacts/publish/<runtime> and artifacts/releases/<runtime>.

.PARAMETER Runtime
    The .NET runtime identifier to build for. Defaults to win-x64.

.PARAMETER Version
    The release version. Defaults to <Version> in VoiceBallGame.App.csproj.

.EXAMPLE
    ./build/pack.ps1
    ./build/pack.ps1 -Runtime linux-x64 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64')]
    [string] $Runtime = 'win-x64',

    [string] $Version
)

$ErrorActionPreference = 'Stop'

# Keep these values synchronized with build/pack.sh.
$PackId = 'VoiceBallGame'
$PackTitle = 'Voice Ball Game'
$PackAuthors = 'Matt Jarvis'
$VpkVersion = '0.0.1298'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot 'src/VoiceBallGame.App/VoiceBallGame.App.csproj'
$Icon = Join-Path $RepoRoot 'src/VoiceBallGame.App/Assets/avalonia-logo.ico'
$PublishDir = Join-Path $RepoRoot "artifacts/publish/$Runtime"
$ReleaseDir = Join-Path $RepoRoot "artifacts/releases/$Runtime"

function Invoke-Checked {
    param([string] $Command, [string[]] $Arguments)

    Write-Host "> $Command $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command exited with code $LASTEXITCODE."
    }
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "vpk was not found. Install it with: dotnet tool install -g vpk --version $VpkVersion"
}

if (-not $Version) {
    $Version = (& dotnet msbuild $Project -getProperty:Version).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $Version) {
        throw 'Could not read <Version> from VoiceBallGame.App.csproj. Pass -Version explicitly.'
    }
}

Write-Host "Packaging $PackId $Version for $Runtime" -ForegroundColor Cyan

# A stale publish folder can include files from an earlier build in the package.
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}

Invoke-Checked dotnet @(
    'publish', $Project,
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', 'true',
    "-p:Version=$Version",
    '-o', $PublishDir
)

$common = @(
    '--packId', $PackId,
    '--packVersion', $Version,
    '--packDir', $PublishDir,
    '--packTitle', $PackTitle,
    '--packAuthors', $PackAuthors,
    '--outputDir', $ReleaseDir
)

switch ($Runtime) {
    'win-x64' {
        Invoke-Checked vpk (@('pack') + $common + @(
            '--runtime', 'win-x64',
            '--mainExe', 'VoiceBallGame.App.exe',
            '--icon', $Icon
        ))
    }
    'linux-x64' {
        Invoke-Checked vpk (@('[linux]', 'pack') + $common + @(
            '--runtime', 'linux-x64',
            '--mainExe', 'VoiceBallGame.App',
            '--categories', 'Education;Science'
        ))
    }
}

Write-Host "Done. Packages are in $ReleaseDir" -ForegroundColor Green

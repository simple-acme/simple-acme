<#
.SYNOPSIS
Compile simple-acme locally with automatic .NET SDK bootstrap.

.DESCRIPTION
Checks for an available dotnet CLI. If unavailable (or when -ForceInstallSdk is
set), downloads Microsoft's dotnet-install script and installs the SDK locally.
Then restores and builds src/wacs.slnx.
#>

[CmdletBinding()]
param(
<<<<<<< codex/add-compilation-process-vpedjm
    [string]$SolutionPath = (Join-Path (Join-Path $PSScriptRoot '..') 'src/wacs.slnx'),
    [string]$InstallDir = (Join-Path (Join-Path $PSScriptRoot '..') '.dotnet'),
=======
    [string]$SolutionPath = (Join-Path $PSScriptRoot '..' 'src/wacs.slnx'),
    [string]$InstallDir = (Join-Path $PSScriptRoot '..' '.dotnet'),
>>>>>>> main
    [string]$SdkChannel = '8.0',
    [switch]$ForceInstallSdk,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "[compile-local] $Message"
}

function Get-DotNetCommand {
    param([string]$ExpectedPath)

    $localDotnet = Join-Path $ExpectedPath 'dotnet'
    if ($IsWindows) {
        $localDotnet = "$localDotnet.exe"
    }

    if (Test-Path -LiteralPath $localDotnet) {
        return $localDotnet
    }

    $globalDotnet = Get-Command -Name 'dotnet' -ErrorAction SilentlyContinue
    if ($globalDotnet) {
        return $globalDotnet.Path
    }

    return $null
}

function Install-DotNetSdk {
    param(
        [string]$Channel,
        [string]$TargetPath
    )

    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null

    $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
    Write-Step "Downloading dotnet-install.ps1 to $installer"
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer

    Write-Step "Installing .NET SDK channel $Channel into $TargetPath"
<<<<<<< codex/add-compilation-process-vpedjm
    & $installer -Channel $Channel -InstallDir $TargetPath -NoPath
=======
    & pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File $installer `
        -Channel $Channel `
        -InstallDir $TargetPath `
        -NoPath
>>>>>>> main

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-install failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $SolutionPath)) {
    throw "Solution file not found: $SolutionPath"
}

$dotnet = if ($ForceInstallSdk) { $null } else { Get-DotNetCommand -ExpectedPath $InstallDir }
if (-not $dotnet) {
    Write-Step '.NET SDK not found; bootstrapping local SDK install.'
    Install-DotNetSdk -Channel $SdkChannel -TargetPath $InstallDir
    $dotnet = Get-DotNetCommand -ExpectedPath $InstallDir
}

if (-not $dotnet) {
    throw "Unable to find dotnet after installation. Expected under: $InstallDir"
}

$env:DOTNET_ROOT = $InstallDir
$env:PATH = "$InstallDir$([IO.Path]::PathSeparator)$env:PATH"

Write-Step "Using dotnet executable: $dotnet"
& $dotnet --info

$buildArgs = @('build', $SolutionPath, '--nologo', '--verbosity', 'minimal')
if ($NoRestore) {
    $buildArgs += '--no-restore'
}

Write-Step "Running: dotnet $($buildArgs -join ' ')"
& $dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Step 'Build succeeded.'

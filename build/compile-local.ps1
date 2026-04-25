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
    [string]$SolutionPath = (Join-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath '..') -ChildPath 'src/wacs.slnx'),
    [string]$InstallDir = (Join-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath '..') -ChildPath '.dotnet'),
    [string]$SdkChannel = '10.0',
    [switch]$ForceInstallSdk,
    [switch]$NoRestore,
    [string]$Runtime     = 'win-x64',
    [switch]$PublishMain
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
    $pwsh = Get-Command -Name 'pwsh' -ErrorAction SilentlyContinue
    if ($pwsh) {
        & $pwsh.Path -NoLogo -NoProfile -ExecutionPolicy Bypass -File $installer `
            -Channel $Channel `
            -InstallDir $TargetPath `
            -NoPath
    } else {
        & $installer -Channel $Channel -InstallDir $TargetPath -NoPath
    }

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

if ($PublishMain) {
    $mainProject = Join-Path -Path (Split-Path -Path $SolutionPath -Parent) -ChildPath 'main\wacs.csproj'
    if (-not (Test-Path -LiteralPath $mainProject)) {
        throw "Main project not found: $mainProject"
    }
    $publishArgs = @('publish', $mainProject, '-c', 'Release', '-r', $Runtime, '--self-contained', '--nologo', '--verbosity', 'minimal')
    Write-Step "Running: dotnet $($publishArgs -join ' ')"
    & $dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE"
    }
    $publishDir = Join-Path -Path (Split-Path -Path $mainProject -Parent) -ChildPath "bin\Release\net10.0\$Runtime\publish"
    Write-Step "Publish output: $publishDir"
}

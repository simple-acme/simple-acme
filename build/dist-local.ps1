[CmdletBinding()]
param(
    [string]$Runtime   = 'win-x64',
    [string]$DistDir   = (Join-Path -Path $PSScriptRoot -ChildPath '..\out\dist-local'),
    [switch]$NoRestore,
    [switch]$SkipPlugins
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "[dist-local] $Message"
}

$repoRoot = Join-Path -Path $PSScriptRoot -ChildPath '..'
$srcRoot = Join-Path -Path $repoRoot -ChildPath 'src'
$mainProject = Join-Path -Path $srcRoot -ChildPath 'main\wacs.csproj'
$mainPublishDir = Join-Path -Path $srcRoot -ChildPath "main\bin\Release\net10.0\$Runtime\publish"
$distSourceDir = Join-Path -Path $repoRoot -ChildPath 'dist'

$localDotnet = Join-Path -Path $repoRoot -ChildPath '.dotnet\dotnet'
if ($IsWindows) {
    $localDotnet = "$localDotnet.exe"
}

$dotnet = $null
if (Test-Path -LiteralPath $localDotnet) {
    $dotnet = $localDotnet
} else {
    $globalDotnet = Get-Command -Name 'dotnet' -ErrorAction SilentlyContinue
    if ($globalDotnet) {
        $dotnet = $globalDotnet.Path
    }
}

if (-not $dotnet) {
    throw "Unable to find dotnet. Run build/compile-local.ps1 first."
}

$mainPublishArgs = @('publish', $mainProject, '-c', 'Release', '-r', $Runtime, '--self-contained', '--nologo', '--verbosity', 'minimal')
if ($NoRestore) {
    $mainPublishArgs += '--no-restore'
}

Write-Step "Running: dotnet $($mainPublishArgs -join ' ')"
& $dotnet @mainPublishArgs
if ($LASTEXITCODE -ne 0) {
    throw "Main publish failed with exit code $LASTEXITCODE"
}

$pluginFolders = @()
if (-not $SkipPlugins) {
    $pluginFolders = Get-ChildItem -Path $srcRoot -Directory -Filter 'plugin.*' |
        Where-Object { $_.Name -notlike 'plugin.common.*' } |
        Where-Object { $_.Name -notlike 'plugin.*.reference' }

    foreach ($pluginFolder in $pluginFolders) {
        $pluginSuffix = $pluginFolder.Name.Substring('plugin.'.Length)
        $pluginProject = Join-Path -Path $pluginFolder.FullName -ChildPath "wacs.$pluginSuffix.csproj"
        $pluginPublishArgs = @('publish', $pluginProject, '-c', 'Release', '--nologo', '--verbosity', 'minimal')
        if ($NoRestore) {
            $pluginPublishArgs += '--no-restore'
        }

        Write-Step "Running: dotnet $($pluginPublishArgs -join ' ')"
        & $dotnet @pluginPublishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Plugin publish failed with exit code $LASTEXITCODE"
        }
    }
}

if (Test-Path -LiteralPath $DistDir) {
    Remove-Item -LiteralPath $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

$mainBinaryName = 'wacs.exe'
if ($Runtime -like 'linux*') {
    $mainBinaryName = 'wacs'
}

$mainBinary = Join-Path -Path $mainPublishDir -ChildPath $mainBinaryName
Copy-Item -LiteralPath $mainBinary -Destination $DistDir

$settingsPath = Join-Path -Path $srcRoot -ChildPath 'main\settings.json'
$settingsDefaultPath = Join-Path -Path $DistDir -ChildPath 'settings_default.json'
Copy-Item -LiteralPath $settingsPath -Destination $settingsDefaultPath

Copy-Item -Path (Join-Path -Path $distSourceDir -ChildPath '*') -Destination $DistDir -Recurse

if (-not $SkipPlugins) {
    foreach ($pluginFolder in $pluginFolders) {
        $pluginPublishDir = Join-Path -Path $pluginFolder.FullName -ChildPath 'bin\Release\net10.0\publish'
        $pluginDlls = Get-ChildItem -Path $pluginPublishDir -Filter '*.dll' -File
        foreach ($pluginDll in $pluginDlls) {
            $targetPath = Join-Path -Path $DistDir -ChildPath $pluginDll.Name
            if (-not (Test-Path -LiteralPath $targetPath)) {
                Copy-Item -LiteralPath $pluginDll.FullName -Destination $targetPath
            }
        }
    }
}

$fileCount = (Get-ChildItem -Path $DistDir -File -Recurse).Count
Write-Step "Distribution folder: $DistDir"
Write-Step "Total files: $fileCount"

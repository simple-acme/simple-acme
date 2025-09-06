$WinGetPath = "..\..\winget-pkgs"
if (-not (Test-Path $WinGetPath)) {
	Push-Location
	cd "..\.."
	git clone https://github.com/simple-acme/winget-pkgs
	Pop-Location
	cd $WinGetPath
	git remote add upstream https://github.com/microsoft/winget-pkgs
}
cd $WinGetPath

# Sync master of our fork with the real master
git fetch upstream
git checkout master
git pull
git merge upstream/master
git push

# Create branch for our update
$branch = "simple-acme-$env:APPVEYOR_REPO_TAG_NAME"
git branch $branch
git checkout $branch

$path = ".\manifests\s\simple-acme\simple-acme\$env:APPVEYOR_BUILD_VERSION\"
if (-not (Test-Path $path)) {
	New-Item $path -Type Directory
}

# Update relevant files
$x86 = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x86.pluggable.zip"
$x64 = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x64.pluggable.zip"
$arm64 = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-arm64.pluggable.zip"
$release = (Get-Item $x64).LastWriteTime
$checksumx86 = (Get-FileHash $x86).Hash
$checksumx64 = (Get-FileHash $x64).Hash
$checksumarm64 = (Get-FileHash $arm64).Hash

$manifest = "
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json

PackageIdentifier: simple-acme.simple-acme
PackageVersion: $env:APPVEYOR_BUILD_VERSION
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.10.0
"
$manifest | Out-File "$path\simple-acme.simple-acme.yaml"

$manifest = "
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json

PackageIdentifier: simple-acme.simple-acme
PackageVersion: $env:APPVEYOR_BUILD_VERSION
InstallerType: zip
NestedInstallerType: portable
NestedInstallerFiles:
- RelativeFilePath: wacs.exe
InstallModes:
- interactive
- silent
UpgradeBehavior: uninstallPrevious
Commands:
- wacs
ReleaseDate: $($release.ToString("yyyy-MM-dd"))
Installers:
- Architecture: x86
  InstallerUrl: https://github.com/simple-acme/simple-acme/releases/download/$env:APPVEYOR_REPO_TAG_NAME/simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x86.pluggable.zip
  InstallerSha256: $checksumx86
- Architecture: x64
  InstallerUrl: https://github.com/simple-acme/simple-acme/releases/download/$env:APPVEYOR_REPO_TAG_NAME/simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x64.pluggable.zip
  InstallerSha256: $checksumx64
- Architecture: arm64
  InstallerUrl: https://github.com/simple-acme/simple-acme/releases/download/$env:APPVEYOR_REPO_TAG_NAME/simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-arm64.pluggable.zip
  InstallerSha256: $checksumarm64
ManifestType: installer
ManifestVersion: 1.10.0
"
$manifest | Out-File "$path\simple-acme.simple-acme.installer.yaml"

$manifest = "
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json

PackageIdentifier: simple-acme.simple-acme
PackageVersion: $env:APPVEYOR_BUILD_VERSION
PackageLocale: en-US
Publisher: simple-acme
PublisherUrl: https://simple-acme.com/
PublisherSupportUrl: https://github.com/simple-acme/simple-acme/issues
Author: Wouter Tinus
PackageName: simple-acme
PackageUrl: https://github.com/simple-acme/simple-acme
License: Apache-2.0
LicenseUrl: https://github.com/simple-acme/simple-acme/blob/main/LICENSE
Copyright: Copyright 2015 Bryan Livingston
CopyrightUrl: https://github.com/simple-acme/simple-acme/blob/main/LICENSE
ShortDescription: A simple cross platform ACME client (for use with Let's Encrypt et al.)
Description: >
  A simple cross platform ACME client - for use with Let's Encrypt. Forked from
  win-acme.
Moniker: wacs
Tags:
- acme
- apache
- automation
- certificates
- cli
- client
- cross-platform
- csharp
- dotnet
- free
- https
- iis
- letsencrypt
- linux
- pem
- rfc8555
ReleaseNotesUrl: https://github.com/simple-acme/simple-acme/releases/tag/$env:APPVEYOR_REPO_TAG_NAME
ManifestType: defaultLocale
ManifestVersion: 1.10.0
"
$manifest | Out-File "$path\simple-acme.simple-acme.locale.en-US.yaml"

# Push new branch to the server
git add .
git commit -m "Update simple-acme to $env:APPVEYOR_REPO_TAG_NAME"
git push --set-upstream origin $branch

# Manual: create PR to upstream fork (maybe use GH.exe?)
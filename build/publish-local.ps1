# Setup local environment
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location
Push-Location $PSScriptFilePath.Directory
. .\environment-local.ps1
. .\01-helpers.ps1

$bundleFile = "$Bundle\signingbundle.zip"
while (!(Test-Path $bundleFile)) {
    Write-Host "File $bundleFile not found, download from SignPath..."
    Read-Host "Press [Enter] to continue"
}
Remove-Item $Final\* -recurse
Decompress $Final $bundleFile

# Gather build metadata
$yaml = Get-Content -Path "$($Final)build.yml" -raw
$env:APPVEYOR_REPO_TAG_NAME = Get-YamlValue "releasetag" $yaml
$env:APPVEYOR_BUILD_VERSION = Get-YamlValue "releasebuild" $yaml
$env:APPVEYOR_REPO_COMMIT = Get-YamlValue "commit" $yaml

# Test if the release is actually signed
Remove-Item $Temp\* -recurse
Decompress $Temp "$Final\simple-acme.v$($env:APPVEYOR_BUILD_VERSION).win-x64.trimmed.zip"
$signed = Get-AuthenticodeSignature "$Temp\wacs.exe"
if ($signed.Status -ne "Valid") {
    Write-Error "The release is not properly signed. Aborting."
    exit 1
}

#.\06-prepare-release.ps1
#.\07-github.ps1
#.\08-nuget.ps1
#\09-docs.ps1
#.\10-chocolatey.ps1
#.\11-winget.ps1

Pop-Location
Pop-Location
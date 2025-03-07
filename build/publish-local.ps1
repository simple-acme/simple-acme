# Setup local environment
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
. .\environment-local.ps1
. .\01-helpers.ps1

$bundle = "$Bundle\signingbundle.zip"
while (!(Test-Path $bundle)) {
    Write-Host "File $bundle not found, download from SignPath..."
    Read-Host "Press [Enter] to continue"
}
Remove-Item $Final\* -recurse
Decompress $Final $bundle

# Gather build metadata
$yaml = Get-Content -Path "$($Final)build.yml" -raw
$env:APPVEYOR_REPO_TAG_NAME = Get-YamlValue "releasetag" $yaml
$env:APPVEYOR_BUILD_VERSION = Get-YamlValue "releasebuild" $yaml
$env:APPVEYOR_REPO_COMMIT = Get-YamlValue "commit" $yaml

#.\06-prepare-release.ps1
#.\07-github.ps1
#.\08-nuget.ps1
#.\09-docs.ps1
.\10-chocolatey.ps1
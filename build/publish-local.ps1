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

$yaml = Get-Content -Path "$($Final)build.yml" -raw
if ($yaml -match "releasetag: ([\S]+)") {
    if (![string]::IsNullOrWhiteSpace($Matches[1])) {
         Write-Host "Tag $($Matches[1]) detected"
        $env:APPVEYOR_REPO_TAG_NAME = $Matches[1]
    } else {
        Write-Host "No tag detected 1"
        exit
    }
} else {
    Write-Host "No tag detected 2"
    exit
}

.\06-prepare-release.ps1
.\07-github.ps1
.\08-nuget.ps1
.\09-docs.ps1
# .\10-chocolatey.ps1
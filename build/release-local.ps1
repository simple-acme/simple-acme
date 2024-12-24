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

# Parse version from file name
$file = (Get-ChildItem $Final "simple-acme.v*" | Select -First 1).Name
if ($file -match "v([0-9\.]+)?\.") {
    $Version = $Matches.1
     Write-Host "Version $Version detected"
} else {
    Write-Host "No version detected"
}

.\06-prepare-release.ps1
.\07-github.ps1

if ($env:nuget -eq "1") {
    .\08-nuget.ps1
}

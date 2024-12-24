# Setup local environment
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
cd ..
cd ..
$bundleFolder = ".\out\bundle"
$bundle = "$bundleFolder\signingbundle.zip"
if (Test-Path $bundle) {
	Remove-Item $bundle -Force
}

# Choose tag
$version = Read-host "Tag (ex. v2.3.0.0)"
git checkout main
git pull
git tag $version
git push origin $version
Write-Host "Wait for build to complete, approve signing at signpath, download the signed bundle..."
Start-Process chrome https://ci.appveyor.com/project/WouterTinus/simple-acme
Start-Process chrome https://app.signpath.io/Web/e396b30d-0bbf-442f-b958-78da3e8c1b7e/SigningRequests
Start-Process explorer $bundleFolder
.\build\publish-local.ps1

# Restore original location
Pop-Location
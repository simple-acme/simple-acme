$ChocoPath = "..\..\chocolatey"
Push-Location $ChocoPath

if ([string]::IsNullOrWhiteSpace($env:ChocoApiKey)) {
	$env:ChocoApiKey = Read-Host "Chocolatey API key"
}

$target = ".\simple-acme\bin\"
if (!(Test-Path $target)) {
	New-Item $target -Type Directory
}
Get-ChildItem $target | Remove-Item
$32bit = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x86.pluggable.zip"
$64bit = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x64.pluggable.zip"
Copy-Item $32bit -Destination $target
Copy-Item $64bit -Destination $target
$checksum32 = (Get-FileHash $32bit).Hash.ToLower()
$checksum64 = (Get-FileHash $64bit).Hash.ToLower()

function Replace-Stuff {
	param($path)
	$content = Get-Content -Path $path
	$content = $content -replace '\$tag(.+=.+)"(.+)"',"`$tag`$1`"$env:APPVEYOR_REPO_TAG_NAME`""
	$content = $content -replace '\$build(.+=.+)"(.+)"',"`$build`$1`"$env:APPVEYOR_BUILD_VERSION`""
	$content = $content -replace 'build:\(.+?\)',"build:(v$env:APPVEYOR_BUILD_VERSION)"
	$content = $content -replace 'commit:\(.+?\)',"commit:($env:APPVEYOR_REPO_COMMIT)"
	$content = $content -replace 'https:\/\/github\.com\/simple-acme\/simple-acme\/releases\/download\/(.+?)\/simple-acme\.[v\.0-9]+\.(.+?)\.(.+?).zip',"https://github.com/simple-acme/simple-acme/releases/download/$env:APPVEYOR_REPO_TAG_NAME/simple-acme.v$env:APPVEYOR_BUILD_VERSION.`$2.`$3.zip"
	$content = $content -replace 'checksum32: .+',"checksum32: $checksum32"
	$content = $content -replace 'checksum64: .+',"checksum64: $checksum64"
	$content = $content -replace '<version>.+</version>',"<version>$($env:APPVEYOR_REPO_TAG_NAME.Replace("v", [string]::Empty))</version>"
	Set-Content -Path $path -Value $content
}

# Update package metadata
git checkout main
git pull
git branch $env:APPVEYOR_REPO_TAG_NAME
git checkout $env:APPVEYOR_REPO_TAG_NAME
git pull

Replace-Stuff ".\simple-acme\tools\chocolateyinstall.ps1"
Replace-Stuff ".\simple-acme\tools\chocolateyuninstall.ps1"
Replace-Stuff ".\simple-acme\tools\VERIFICATION.txt"
Replace-Stuff ".\simple-acme\simple-acme.nuspec"

git add .
git commit -m "Release $env:APPVEYOR_REPO_TAG_NAME"
git push --set-upstream origin $env:APPVEYOR_REPO_TAG_NAME

Remove-Item "*.nupkg" 

choco pack .\simple-acme\simple-acme.nuspec
choco push --source https://push.chocolatey.org/ --apikey $env:ChocoApiKey

Pop-Location
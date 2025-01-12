$ChocoPath = "..\..\chocolatey"
Push-Location $ChocoPath

if ([string]::IsNullOrWhiteSpace($env:ChocoApiKey)) {
	$env:ChocoApiKey = Read-Host "Chocolatey API key"
}

$target = ".\simple-acme\bin\"
if (!(Test-Path $target)) {
	New-Item $target -Type Directory
}
Copy-Item $Final\simple-acme.$env:APPVEYOR_REPO_TAG_NAME.win-x86.pluggable.zip -Destination $target
Copy-Item $Final\simple-acme.$env:APPVEYOR_REPO_TAG_NAME.win-x64.pluggable.zip -Destination $target
choco pack .\simple-acme\simple-acme.nuspec
# choco push --source https://push.chocolatey.org/ --apikey $env:ChocoApiKey


Pop-Location
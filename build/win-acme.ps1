function Convert($type, $arch) {
	Remove-Item $Temp\* -recurse
	Decompress $Temp "$Final\simple-acme.v$($env:APPVEYOR_BUILD_VERSION).win-$arch.$type.zip"
	$DefaultSettingsFile = "$Temp\settings_default.json"
	$config = Get-Content $DefaultSettingsFile -Raw | ConvertFrom-Json 
	$config.Client.ClientName = "win-acme"
	$config.Acme.DefaultBaseUri = "https://acme.zerossl.com/v2/DV90/"
	$config.Acme.DefaultBaseUriTest = $null
	$config.Acme.DefaultBaseUriImport = $null
	Set-Content $DefaultSettingsFile ($config | ConvertTo-Json -Depth 10)
	Compress $Temp "$Final\win-acme.v$($env:APPVEYOR_BUILD_VERSION).$arch.$type.zip"
}

Convert "pluggable" "x64"
Convert "pluggable" "x86"
Convert "pluggable" "arm64"
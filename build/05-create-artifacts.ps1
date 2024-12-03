function PlatformRelease
{
	param($Config, $Platform)

	Remove-Item $Temp\* -recurse
	$Postfix = "pluggable"
	if ($Config -eq "ReleaseTrimmed") {
		$Postfix = "trimmed"
	}
	$MainZip = "simple-acme.v$Version.$Platform.$Postfix.zip"
	$MainZipPath = "$Out\$MainZip"
	$MainBinDir = BuildPath "$Root\src\main\bin\$Config\$NetVersion\$Platform"
	$MainBinFile = "wacs.exe"
	if ($Platform -like "linux*") {
		$MainBinFile = "wacs"
	}
	# Code signing
	if ($Platform -like "win*") {
		if (-not [string]::IsNullOrEmpty($SelfSigningPassword)) {
			./sign-selfsigned.ps1 `
				-Path "$MainBinDir\publish\$MainBinFile" 
				-Pfx "$Root\build\codesigning.pfx" `
				-Password $SelfSigningPassword
		}
	}

	Copy-Item "$MainBinDir\publish\$MainBinFile" $Temp
	if ($Platform -like "linux*") {
		Copy-Item "$MainBinDir\settings.linux.json" "$Temp\settings_default.json"
	} else {
		Copy-Item "$MainBinDir\settings.json" "$Temp\settings_default.json"
	}
	Copy-Item "$Root\dist\*" $Temp -Recurse
	Set-Content -Path "$Temp\version.txt" -Value "v$Version ($Platform, $Config)"
	Compress $Temp $MainZipPath
	Remove-Item $Temp\* -recurse

	# Managed debugger interface as optional extra download
	if ($Platform -like "win*") {
		$DbiZip = "mscordbi.v$Version.$Platform.zip"
		$DbiZipPath = "$Out\$DbiZip"
		if (!(Test-Path $DbiZipPath)) {
			CreateArtifact $MainBinDir @("mscordbi.dll") $DbiZipPath
		}
	}
}

function CreateArtifact {
	param($Dir, $Files, $Target)
	Remove-Item $Temp\* -recurse
	foreach ($file in $files) {
		Copy-Item "$Dir\$file" $Temp
	}
	Compress $Temp $Target
	Remove-Item $Temp\* -recurse
}

function PluginRelease
{
	param($Dir, $Files, $Folder)

	$PlugZip = "$Dir.v$Version.zip"
	$PlugZipPath = "$Out\$PlugZip"
	$PlugBin = $Folder

	CreateArtifact $PlugBin $Files $PlugZipPath

	# Special for the FTP plugin
	if ($Dir -eq "plugin.validation.http.ftp") {
		$GnuTlsZip = "gnutls.v$Version.x64.zip"
		$GnuTlsZipPath = "$Out\$GnuTlsZip"
		$GnuTlsSrc = $PlugBin
		if (!(Test-Path $GnuTlsZipPath)) {
			CreateArtifact $GnuTlsSrc @(
				"libgcc_s_seh-1.dll",
				"libgmp-10.dll",
				"libgnutls-30.dll",
				"libhogweed-6.dll",
				"libnettle-8.dll",
				"libwinpthread-1.dll") $GnuTlsZipPath
		}
	}
}

function NugetRelease
{
	$PackageFolder = "$Root\src\main\nupkg"
	if (Test-Path $PackageFolder)
	{
		Copy-Item "$PackageFolder\*" $Out -Recurse
	}
}

function Arguments
{
	param($plugins)
	if (-not ($Configs -contains "Release")) {
		return
	}
	if (-not ($Platforms -contains "win-x64")) {
		return
	}
	Status "Generate docs YML"
	$MainBinDir = BuildPath "$Root\src\main\bin\Release\$NetVersion\win-x64"
	foreach ($plugin in $plugins) {
		foreach ($file in $plugin.files) {
			Copy-Item "$($plugin.folder)\$file" $MainBinDir
		}
	}
	$path = "$MainBinDir\wacs.exe"
	$parms = "--docs --verbose".Split(" ")
	Push-Location $out
	& "$path" $parms | Out-Null
	Pop-Location
}

if ($BuildNuget) {
	NugetRelease
}

foreach ($config in $configs) {
	foreach ($platform in $platforms) {
		Status "Package $platform $config..."
		PlatformRelease $config $platform 
	}
}

if ($BuildPlugins) {
	$plugins = Import-CliXml -Path $Root\out\plugins.xml
	foreach ($plugin in $plugins) {
		Status "Package $($plugin.Name)"
		PluginRelease $plugin.name $plugin.files $plugin.folder
	}
}
Arguments $plugins
Status "Artifacts created!"
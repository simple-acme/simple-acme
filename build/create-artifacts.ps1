param (
	[Parameter(Mandatory=$true)]
	[string]
	$Root,

	[Parameter(Mandatory=$true)]
	[string]
	$Version,

	[Parameter(Mandatory=$true)]
	[string]
	$NetVersion,

	[string]
	$SigningPassword,

	[Parameter(Mandatory=$true)]
	[string[]]
	$Configs,

	[Parameter(Mandatory=$true)]
	[string[]]
	$Platforms,

	[Parameter(Mandatory=$true)]
	[switch]
	$BuildPlugins,

	[Parameter(Mandatory=$true)]
	[switch]
	$BuildNuget
)

Add-Type -Assembly "system.io.compression.filesystem"
$Temp = "$Root\build\temp\"
$Out = "$Root\build\artifacts\"

if (Test-Path $Temp)
{
    Remove-Item $Temp -Recurse
}
New-Item $Temp -Type Directory

if (Test-Path $Out)
{
    Remove-Item $Out -Recurse
}
New-Item $Out -Type Directory

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
	$MainBinDir = "$Root\src\main\bin\$Config\$NetVersion\$Platform"
	if (!(Test-Path $MainBinDir))
	{
		# For some reason AppVeyor generates paths like this instead of the above on local systems
		$MainBinDir = "$Root\src\main\bin\Any CPU\$Config\$NetVersion\$Platform"
	}
	$MainBinFile = "wacs.exe"
	if ($Platform -like "linux*") {
		$MainBinFile = "wacs"
	}
	if (Test-Path $MainBinDir)
	{
		if (($Platform -like "win*") -and (-not [string]::IsNullOrEmpty($SigningPassword))) {
			./sign-exe.ps1 "$MainBinDir\publish\$MainBinFile" "$Root\build\codesigning.pfx" $SigningPassword
		}
		Copy-Item "$MainBinDir\publish\$MainBinFile" $Temp
		if ($Platform -like "linux*") {
			Copy-Item "$MainBinDir\settings.linux.json" "$Temp\settings_default.json"
		} else {
			Copy-Item "$MainBinDir\settings.json" "$Temp\settings_default.json"
		}
		Copy-Item "$Root\dist\*" $Temp -Recurse
		Set-Content -Path "$Temp\version.txt" -Value "v$Version ($Platform, $Config)"
		[io.compression.zipfile]::CreateFromDirectory($Temp, $MainZipPath)
	}

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
	[io.compression.zipfile]::CreateFromDirectory($Temp, $Target)
}

function PluginRelease
{
	param($Dir, $Files, $Folder)

	Remove-Item $Temp\* -recurse
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

if ($BuildNuget) {
	NugetRelease
}

foreach ($config in $configs) {
	foreach ($platform in $platforms) {
		PlatformRelease $config $platform 
	}
}

if ($BuildPlugins) {
	$plugins = Import-CliXml -Path $Root\build\plugins.xml
	foreach ($plugin in $plugins) {
		PluginRelease $plugin.name $plugin.files $plugin.folder
	}
}

"Created artifacts:"
dir $Out
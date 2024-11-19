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
	$SelfSigningPassword,
	
	[string]
	$SignPathApiToken,

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
New-Item $Temp -Type Directory | Out-Null

if (Test-Path $Out)
{
    Remove-Item $Out -Recurse
}
New-Item $Out -Type Directory | Out-Null

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
	$MainBinDir = MainBinDir $Config $Platform
	$MainBinFile = "wacs.exe"
	if ($Platform -like "linux*") {
		$MainBinFile = "wacs"
	}
	if (Test-Path $MainBinDir)
	{
		# Code signing
		if (($Platform -like "win*") -and (-not [string]::IsNullOrEmpty($SelfSigningPassword))) {
			./sign-selfsigned.ps1 `
				-Path "$MainBinDir\publish\$MainBinFile" 
				-Pfx "$Root\build\codesigning.pfx" `
				-Password $SelfSigningPassword
		}
		elseif (-not [string]::IsNullOrEmpty($SignPathApiToken)) {
			./sign-signpath.ps1 `
				-Path "$MainBinDir\publish\$MainBinFile" `
				-ApiToken $SignPathApiToken `
				-Version $version
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

function Compress {
	param($Dir, $Target)
	[io.compression.zipfile]::CreateFromDirectory($Temp, $Target)
	$targetInfo = Get-Item $Target
	$global:yaml += "
  - 
    name: $($targetInfo.Name)
    size: $(Get-FriendlySize $targetInfo.Length)
    sha256: $((Get-FileHash $targetInfo).Hash)"
}

function CreateArtifact {
	param($Dir, $Files, $Target)

	Remove-Item $Temp\* -recurse
	foreach ($file in $files) {
		Copy-Item "$Dir\$file" $Temp
	}
	Compress $Temp $Target
}

function Get-FriendlySize {
    param($Bytes)
    $sizes='Bytes,KB,MB,GB,TB,PB,EB,ZB' -split ','
    for($i=0; ($Bytes -ge 1kb) -and
        ($i -lt $sizes.Count); $i++) {$Bytes/=1kb}
    $N=1; if($i -eq 0) {$N=0}
    "{0:N$($N)} {1}" -f $Bytes, $sizes[$i]
}

function PluginRelease
{
	param($Dir, $Files, $Folder)

	Remove-Item $Temp\* -recurse
	$PlugZip = "$Dir.v$Version.zip"
	$PlugZipPath = "$Out\$PlugZip"
	$PlugBin = $Folder

	# Sign plugin
	if (-not [string]::IsNullOrEmpty($SignPathApiToken)) {
		foreach ($child in Get-ChildItem $Folder -Filter "PKISharp.WACS.*.dll") {
			./sign-signpath.ps1 `
				-Path $child `
				-ApiToken $SignPathApiToken `
				-Version $version
		}
	}

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

function MainBinDir
{
	param($Config, $Platform)

	$MainBinDir = "$Root\src\main\bin\$Config\$NetVersion\$Platform"
	if (!(Test-Path $MainBinDir))
	{
		# For some reason AppVeyor generates paths like this instead of the above on local systems
		$MainBinDir = "$Root\src\main\bin\Any CPU\$Config\$NetVersion\$Platform"
	}
	return $MainBinDir
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
	Write-Host ""
	Write-Host "------------------------------------" -ForegroundColor Green
	Write-Host "Generate YML"						  -ForegroundColor Green
	Write-Host "------------------------------------" -ForegroundColor Green
	Write-Host ""
	$MainBinDir = MainBinDir "Release" "win-x64"
	foreach ($plugin in $plugins) {
		foreach ($file in $plugin.files) {
			Copy-Item "$($plugin.folder)\$file" $MainBinDir
		}
	}
	$path = "$MainBinDir\wacs.exe"
	$parms = "--docs --verbose".Split(" ")
	& "$path" $parms | Out-Null
	Copy-Item $root\build\arguments.yml "$($out)arguments.yml"
	Copy-Item $root\build\plugins.yml "$($out)plugins.yml"
}

if ($BuildNuget) {
	NugetRelease
}

$global:yaml = "
releasename: $Version
releasetag: $Version
releasebuild: $Version
downloads: "

foreach ($config in $configs) {
	foreach ($platform in $platforms) {
		Write-Host ""
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host "Package $platform $config..."		  -ForegroundColor Green
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host ""
		PlatformRelease $config $platform 
	}
}

if ($BuildPlugins) {
	$plugins = Import-CliXml -Path $Root\build\plugins.xml
	foreach ($plugin in $plugins) {
		Write-Host ""
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host "Package $($plugin.Name)"			  -ForegroundColor Green
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host ""
		PluginRelease $plugin.name $plugin.files $plugin.folder
	}
	Arguments $plugins
}

Set-Content -Path "$($out)build.yml" -Value $global:yaml

Write-Host ""
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host "Hashes straight from the press" 	  -ForegroundColor Green
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host ""
$global:yaml

Write-Host ""
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host "Artifacts created!"					  -ForegroundColor Green
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host ""


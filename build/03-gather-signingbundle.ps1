param (
	[Parameter(Mandatory=$true)]
	[string]
	$Root,

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

$Temp = "$Root\out\temp\"
$Out = "$Root\out\artifacts\"
EnsureFolder $Temp
EnsureFolder $Out

function PlatformRelease
{
	param($Config, $Platform)

	if (-not ($Platform -like "win*")) {
		return
	}
	$MainBinDir = BuildPath "$Root\src\main\bin\$Config\$NetVersion\$Platform"
	$target = "$Temp\$config\$platform"
	New-Item $target -Type Directory | Out-Null
	Copy-Item "$MainBinDir\publish\wacs.exe" $target
}

function PluginRelease
{
	param($Dir, $Files, $Folder)
	$target = "$Temp\plugins"
	New-Item $target -Type Directory | Out-Null
	foreach ($child in (Get-ChildItem $Folder -Filter "PKISharp.WACS.*.dll").FullName) {
		Copy-Item $child $target
	}
}

function NugetRelease
{
	$PackageFolder = "$Root\src\main\nupkg"
	$target = "$Temp\nuget"
	New-Item $target -Type Directory | Out-Null
	foreach ($child in (Get-ChildItem $PackageFolder -Filter "*.nupkg").FullName) {
		Copy-Item $child $target
	}
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

if ($BuildNuget) {
	NugetRelease
}

Compress $Temp "$Out\signingbundle.zip"
Status "Signing bundle created!"
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
	Copy-Item "$target\wacs.exe" "$MainBinDir\publish" 
}

function PluginRelease
{
	param($Dir, $Files, $Folder)
	$target = "$Temp\plugins"
	foreach ($child in (Get-ChildItem $target -Filter "PKISharp.WACS.*.dll").FullName) {
		Copy-Item $child $Folder
	}
}

function NugetRelease
{
	$PackageFolder = "$Root\src\main\nupkg"
	$target = "$Temp\nuget"
	foreach ($child in (Get-ChildItem $target -Filter "*.nupkg").FullName) {
		Copy-Item $child $PackageFolder
	}
}

Decompress "$Out\signingbundle.zip" $Temp 
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

Status "Signed results redistributed!"
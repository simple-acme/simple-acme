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

Remove-Item $Temp\* -recurse
Decompress $Temp "$Out\signingbundle.zip"
foreach ($config in $configs) {
	foreach ($platform in $platforms) {
		PlatformRelease $config $platform 
	}
}
if ($BuildPlugins) {
	$plugins = Import-CliXml -Path $Root\out\plugins.xml
	foreach ($plugin in $plugins) {
		PluginRelease $plugin.name $plugin.files $plugin.folder
	}
}
if ($BuildNuget) {
	NugetRelease
}
Remove-Item $Temp\* -recurse

Status "Signed results redistributed!"
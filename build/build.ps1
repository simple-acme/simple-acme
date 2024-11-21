param (
	[Parameter(Mandatory=$true)]
	[ValidatePattern("^\d+\.\d+.\d+.\d+")]
	[string]
	$Version,

	[Parameter(Mandatory=$true)]
	[string[]]
	$Configs,

	[Parameter(Mandatory=$true)]
	[string[]]
	$Platforms,

	[Parameter(Mandatory=$true)]
	[string]
	$NetVersion,

	[switch]
	$BuildPlugins = $false,

	[int]
	$BuildPluginsCount = 1000,

	[switch]
	$BuildNuget = $false,

	[switch]
	$Clean = $false,

	[string]
	$SelfSigningPassword
)

try {
	cls
} catch {
	# Ignore
}

$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName

# Restore NuGet packages
& dotnet restore $RepoRoot\src\main\wacs.csproj

# Clean solution
if ($Clean) {
	foreach ($platform in $platforms) 
	{
		foreach ($config in $configs) 
		{
			Status "Clean $platform $config..."
			& dotnet clean $RepoRoot\src\main\wacs.csproj -c $release -r $arch /p:SelfContained=true
		}
	}
}

# Build Nuget package
if ($BuildNuget) {	
	Status "Publish NuGet..."
	& dotnet pack $RepoRoot\src\main\wacs.csproj -c "Release" /p:PublishSingleFile=false /p:PublishReadyToRun=false
}

# Build regular releases
foreach ($platform in $platforms) 
{
	foreach ($config in $configs) 
	{
		Status "Publish $platform $config..."

		$extra = ""
		if ($config.EndsWith("Trimmed")) {
			$extra = "/p:warninglevel=0"
		}
		& dotnet publish $RepoRoot\src\main\wacs.csproj -c $config -r $platform --self-contained $extra
		if (-not $?)
		{
			Pop-Location
			throw "The dotnet publish process returned an error code."
		}
	}
}

# Build plugins
if ($BuildPlugins) {
	Status "Build reference project..."

	& dotnet publish $RepoRoot\src\main.lib\wacs.lib.csproj -c Release -r "win-x64"
	$referenceDir = BuildPath "$RepoRoot\src\main.lib\bin\Release\$NetVersion\win-x64\Publish"
	$referenceFiles = (Get-ChildItem $ReferenceDir).Name
	if (-not $?)
	{
		Pop-Location
		throw "The dotnet publish process returned an error code."
	}

	# Detect all plugins
	$pluginFolders = (Get-ChildItem $RepoRoot\src\ plugin.*).Name
	$plugins = $pluginFolders | `
		Where-Object { -not ($_ -like "*.common.*") } | `
		ForEach-Object { @{ Name = $_; Files = @(); Folder = "" } } | `
		Select-Object -First $BuildPluginsCount

	foreach ($plugin in $plugins) 
	{
		Status "Publish $($plugin.Name)..."

		$project = $plugin.Name.Replace("plugin.", "")
		& dotnet publish $RepoRoot\src\$($plugin.Name)\wacs.$project.csproj -c "Release"
		if (-not $?)
		{
			Pop-Location
			throw "The dotnet publish process returned an error code."
		}
		$pluginDir = BuildPath "$RepoRoot\src\$($plugin.Name)\bin\Release\$NetVersion\publish"
		$pluginFiles = (Get-ChildItem $pluginDir *.dll).Name
		$plugin.Files = $pluginFiles | Where-Object { -not ($referenceFiles -contains $_) }
		$plugin.Folder = $pluginDir
		Write-Host "Detected files: " $plugin.Files -ForegroundColor Green
	}

	# Save plugin metadata for create-artifacts script
	Export-CliXml -InputObject $plugins -Path $RepoRoot\build\plugins.xml
}

Status "Build complete!"
Pop-Location
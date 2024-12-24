# Clear previous build results
ClearFolders

# Write metadata
$yaml = "releasename: $env:APPVEYOR_REPO_TAG_NAME
releasetag: $env:APPVEYOR_REPO_TAG_NAME
releasebuild: $env:APPVEYOR_BUILD_VERSION
commit: $env:APPVEYOR_REPO_COMMIT"
Set-Content -Path "$($Out)build.yml" -Value $yaml

# Restore NuGet packages
& dotnet restore $Root\src\main\wacs.csproj

# Clean solution
if ($Clean) {
	foreach ($platform in $platforms) 
	{
		foreach ($config in $configs) 
		{
			Status "Clean $platform $config..."
			& dotnet clean $Root\src\main\wacs.csproj -c $release -r $arch /p:SelfContained=true
		}
	}
}

# Build Nuget package
if ($BuildNuget) {	
	Status "Publish NuGet..."
	& dotnet pack $Root\src\main\wacs.csproj -c "Release" /p:PublishSingleFile=false /p:PublishReadyToRun=false
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
		& dotnet publish $Root\src\main\wacs.csproj -c $config -r $platform --self-contained $extra
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

	& dotnet publish $Root\src\main.lib\wacs.lib.csproj -c Release -r "win-x64"
	$referenceDir = BuildPath "$Root\src\main.lib\bin\Release\$NetVersion\win-x64\Publish"
	$referenceFiles = (Get-ChildItem $ReferenceDir).Name
	if (-not $?)
	{
		Pop-Location
		throw "The dotnet publish process returned an error code."
	}

	# Detect all plugins
	$pluginFolders = (Get-ChildItem $Root\src\ plugin.*).Name
	$plugins = $pluginFolders | `
		Where-Object { -not ($_ -like "*.common.*") } | `
		ForEach-Object { @{ Name = $_; Files = @(); Folder = "" } } | `
		Select-Object -First $BuildPluginsCount

	foreach ($plugin in $plugins) 
	{
		Status "Publish $($plugin.Name)..."

		$project = $plugin.Name.Replace("plugin.", "")
		& dotnet publish $Root\src\$($plugin.Name)\wacs.$project.csproj -c "Release"
		if (-not $?)
		{
			Pop-Location
			throw "The dotnet publish process returned an error code."
		}
		$pluginDir = BuildPath "$Root\src\$($plugin.Name)\bin\Release\$NetVersion\publish"
		$pluginFiles = (Get-ChildItem $pluginDir *.dll).Name
		$plugin.Files = $pluginFiles | Where-Object { -not ($referenceFiles -contains $_) }
		$plugin.Folder = $pluginDir
		Write-Host "Detected files: " $plugin.Files -ForegroundColor Green
	}

	# Save plugin metadata for create-artifacts script
	Export-CliXml -InputObject $plugins -Path $Root\out\plugins.xml
}

Status "Build complete!"
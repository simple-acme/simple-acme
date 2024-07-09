param (
	[ValidatePattern("^\d+\.\d+.\d+.\d+")]
	[string]
	$Version = "2.0.0.0",

	[string[]]
	$Configs = @("ReleaseTrimmed"),

	[string[]]
	$Platforms = @("win-x64"),

	[string]
	$NetVersion = "net8.0",

	[switch]
	$BuildPlugins = $false,

	[switch]
	$BuildNuget = $false,

	[switch]
	$Clean = $true,

	[switch]
	$CreateArtifacts = $true,

	[string]
	$SigningPassword
)

try {
	cls
} catch {
	# Ignore
}

$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot "build"

# Restore NuGet packages
& dotnet restore $RepoRoot\src\main\wacs.csproj

# Clean solution
if ($Clean) {
	foreach ($platform in $platforms) 
	{
		foreach ($config in $configs) 
		{
			Write-Host ""
			Write-Host "------------------------------------" -ForegroundColor Green
			Write-Host "Clean $platform $config...		    " -ForegroundColor Green
			Write-Host "------------------------------------" -ForegroundColor Green
			Write-Host ""
			& dotnet clean $RepoRoot\src\main\wacs.csproj -c $release -r $arch /p:SelfContained=true
		}
	}
}


# Build Nuget package
if ($BuildNuget) {
			
	Write-Host ""
	Write-Host "------------------------------------" -ForegroundColor Green
	Write-Host "Publish Nuget...				    " -ForegroundColor Green
	Write-Host "------------------------------------" -ForegroundColor Green
	Write-Host ""

	& dotnet pack $RepoRoot\src\main\wacs.csproj -c "Release" /p:PublishSingleFile=false /p:PublishReadyToRun=false
}

# Build regular releases
foreach ($platform in $platforms) 
{
	foreach ($config in $configs) 
	{
		Write-Host ""
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host "Publish $platform $config...		    " -ForegroundColor Green
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host ""

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

	Write-Host ""
	Write-Host "------------------------------------" -ForegroundColor Green
	Write-Host "Build reference project..."			  -ForegroundColor Green
	Write-Host "------------------------------------" -ForegroundColor Green
	Write-Host ""

	& dotnet publish $RepoRoot\src\main.lib\wacs.lib.csproj -c Release -r "win-x64"
	$referenceDir = "$RepoRoot\src\main.lib\bin\Release\$NetVersion\win-x64\Publish"
	if (!(Test-Path $referenceDir))
	{
		# For some reason AppVeyor generates paths like this instead of the above on local systems
		$referenceDir = "$RepoRoot\src\main.lib\bin\Any CPU\Release\$NetVersion\win-x64\Publish"
	}
	$referenceFiles = (Get-ChildItem $ReferenceDir).Name
	if (-not $?)
	{
		Pop-Location
		throw "The dotnet publish process returned an error code."
	}

	# Detect all plugins
	$pluginFolders = (Get-ChildItem $RepoRoot\src\ plugin.*).Name
	$plugins = $pluginFolders | ForEach-Object { @{ Name = $_; Files = @(); Folder = "" } }
	foreach ($plugin in $plugins) 
	{
		if ($plugin.Name -like "*.common.*") {
			continue;
		}

		Write-Host ""
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host "Publish $($plugin.Name)..."			  -ForegroundColor Green
		Write-Host "------------------------------------" -ForegroundColor Green
		Write-Host ""

		$project = $plugin.Name.Replace("plugin.", "")
		& dotnet publish $RepoRoot\src\$($plugin.Name)\wacs.$project.csproj -c "Release"
		if (-not $?)
		{
			Pop-Location
			throw "The dotnet publish process returned an error code."
		}
		$pluginDir = "$RepoRoot\src\$($plugin.Name)\bin\Release\$NetVersion\publish"
		if (!(Test-Path $pluginDir))
		{
			# For some reason AppVeyor generates paths like this instead of the above on local systems
			$pluginDir = "$RepoRoot\src\$($plugin.Name)\bin\Any CPU\Release\$NetVersion\publish"
		}
		$pluginFiles = (Get-ChildItem $RepoRoot\src\$($plugin.Name)\bin\Release\$NetVersion\publish *.dll).Name
		$plugin.Files = $pluginFiles | Where-Object { -not ($referenceFiles -contains $_) }
		$plugin.Folder = $pluginDir
		Write-Host "Detected files: " $plugin.Files -ForegroundColor Green
	}

	# Save plugin metadata for create-artifacts script
	Export-CliXml -InputObject $plugins -Path $RepoRoot\build\plugins.xml
}

Write-Host ""
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host "Build complete!"					  -ForegroundColor Green
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host ""

if ($CreateArtifacts) 
{
	./create-artifacts.ps1 -Root $RepoRoot -Version $Version -NetVersion $NetVersion -Configs $Configs -Platforms $Platforms -BuildNuget:$BuildNuget -BuildPlugins:$BuildPlugins -SigningPassword $SigningPassword
}
Pop-Location
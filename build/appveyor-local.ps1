param (
	[string]
	$SelfSigningPassword
)
# Environment variables
$env:APPVEYOR_BUILD_VERSION = "2.3.0.0"
$env:NetVersion = "net8.0"
$env:Configs = "Release"
$env:Platforms = "win-x64"
$env:Clean = 0 
$env:Nuget = 0 
$env:Plugins = 0 
$env:PluginsCount = 1

# Make sure we're working from the right folder
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$env:APPVEYOR_BUILD_FOLDER = $PSScriptFilePath.Directory.Parent.FullName

# AppVeyor: before_build
. .\01-helpers.ps1

# AppVeyor: build_script
.\02-build.ps1

# AppVeyor: after_build
.\03-gather-signingbundle.ps1

# AppVeyor: after_deploy
.\04-scatter-signingbundle.ps1
.\05-create-artifacts.ps1
.\07-create-artifactmeta.ps1
.\08-clean.ps1

Status "All done!"
Pop-Location
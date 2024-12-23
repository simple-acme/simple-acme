# Environment variables
$env:APPVEYOR_BUILD_VERSION = "2.3.0.0"
$env:APPVEYOR_REPO_TAG = $true
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
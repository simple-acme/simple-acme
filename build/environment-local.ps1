# Environment variables
$env:APPVEYOR_BUILD_VERSION = "2.3.0.0"
$env:APPVEYOR_REPO_TAG = $true
$env:NetVersion = "net8.0"
$env:Configs = @("Release", "ReleaseTrimmed")
$env:Platforms = @("win-x64", "win-x86", "win-arm64", "linux-x64", "linux-arm64")
$env:Clean = 0 
$env:Nuget = 1 
$env:Plugins = 1 
$env:PluginsCount = 999

# Make sure we're working from the right folder
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$env:APPVEYOR_BUILD_FOLDER = $PSScriptFilePath.Directory.Parent.FullName
# Environment variables
$env:APPVEYOR_BUILD_VERSION = "2.3.0.1234"
$env:APPVEYOR_REPO_TAG_NAME = "v2.3.1-signtest"
$env:APPVEYOR_REPO_TAG = $true
$env:APPVEYOR_REPO_COMMIT = (git rev-parse HEAD)
$env:NetVersion = "net9.0"
$env:Configs = "Release" # @("Release", "ReleaseTrimmed")
$env:Platforms = "win-x64" # @("win-x64", "win-x86", "win-arm64", "linux-x64", "linux-arm64")
$env:Clean = 0 
$env:Nuget = 0 
$env:Plugins = 1 
$env:PluginsCount = 1 # 999

# Make sure we're working from the right folder
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$env:APPVEYOR_BUILD_FOLDER = $PSScriptFilePath.Directory.Parent.FullName
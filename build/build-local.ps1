param (
	[string]
	$SelfSigningPassword
)
# Environment variables
$version = "2.3.0.0"
$netVersion = "net8.0"
$configs = ("Release", "ReleaseTrimmed")
$platforms = ("win-x64", "win-x86")
$clean = $false 
$nuget = $true 
$plugins = $true 
$pluginsCount = 1

# Make sure we're working from the right folder
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName

# AppVeyor: before_build
. .\helpers.ps1

# Always start with a fresh folder
ClearFolder "$RepoRoot\build\artifacts\"
ClearFolder "$RepoRoot\build\signingbundle\"
ClearFolder "$RepoRoot\build\temp\"

# AppVeyor: build_script
.\build.ps1 `
    -Version $version `
    -NetVersion $netVersion `
    -Configs $configs `
    -Platforms $platforms `
    -Clean:$clean `
    -BuildNuget:$nuget `
    -BuildPlugins:$plugins `
    -BuildPluginsCount $pluginsCount `
    -SelfSigningPassword $SelfSigningPassword

# AppVeyor: after_build
.\create-signingbundle.ps1 `
    -Root $RepoRoot `
    -Configs $configs `
    -Platforms $platforms `
    -BuildNuget:$nuget `
    -BuildPlugins:$plugins

# AppVeyor: after_deploy
.\create-artifacts.ps1 `
    -Root $RepoRoot `
    -Version $version `
    -NetVersion $netVersion `
    -Configs $configs `
    -Platforms $platforms `
    -BuildNuget:$nuget `
    -BuildPlugins:$plugins
.\create-artifactmeta.ps1 `
    -Root $RepoRoot `
    -Version $version

Status "All done!"
Pop-Location
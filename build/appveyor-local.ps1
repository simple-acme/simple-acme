param (
	[string]
	$SelfSigningPassword
)
# Environment variables
$version = "2.3.0.0"
$netVersion = "net8.0"
$configs = ("Release")
$platforms = ("win-x64")
$clean = $false 
$nuget = $true 
$plugins = $true 
$pluginsCount = 1

# Make sure we're working from the right folder
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName

# AppVeyor: before_build
. .\01-helpers.ps1

# Always start with a fresh folder
ClearFolder "$RepoRoot\out\artifacts\"
ClearFolder "$RepoRoot\out\signingbundle\"
ClearFolder "$RepoRoot\out\temp\"

# AppVeyor: build_script
.\02-build.ps1 `
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
.\03-gather-signingbundle.ps1 `
    -Root $RepoRoot `
    -Configs $configs `
    -Platforms $platforms `
    -BuildNuget:$nuget `
    -BuildPlugins:$plugins

# AppVeyor: after_deploy
.\04-scatter-signingbundle.ps1 `
    -Root $RepoRoot `
    -Configs $configs `
    -Platforms $platforms `
    -BuildNuget:$nuget `
    -BuildPlugins:$plugins
.\05-create-artifacts.ps1 `
    -Root $RepoRoot `
    -Version $version `
    -NetVersion $netVersion `
    -Configs $configs `
    -Platforms $platforms `
    -BuildNuget:$nuget `
    -BuildPlugins:$plugins
.\07-create-artifactmeta.ps1 `
    -Root $RepoRoot `
    -Version $version
.\08-clean.ps1

Status "All done!"
Pop-Location
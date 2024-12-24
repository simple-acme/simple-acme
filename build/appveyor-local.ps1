param (
	[string]
	$SelfSigningPassword
)

# Setup local environment
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
. .\environment-local.ps1

# AppVeyor: before_build
. .\01-helpers.ps1

# AppVeyor: build_script
.\02-build.ps1

# AppVeyor: after_build
.\03-create-artifacts.ps1
if ($env:APPVEYOR_REPO_TAG) {
	.\04-gather-signingbundle.ps1
}

Status "Build complete!"
explorer "..\out\"
Pop-Location
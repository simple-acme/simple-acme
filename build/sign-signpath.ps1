<#
# Use the following command to create a self-signed cert to build a signed version of the WACS executable 
New-SelfSignedCertificate `
    -CertStoreLocation cert:\currentuser\my `
    -Subject "CN=WACS" `
    -KeyUsage DigitalSignature `
    -Type CodeSigning `
	-NotAfter (Get-Date).AddMonths(24) 
#>

param (
	[Parameter(Mandatory=$true)]
	[string]
	$Path,
	
	[Parameter(Mandatory=$true)]
	[string]
	$ApiToken,

	[Parameter(Mandatory=$true)]
	[string]
	$Version,

    [Parameter()]
	[string]
	$BuildSettings,

    [Parameter()]
	[string]
	$BuildUrl = "https://ci.appveyor.com/project/WouterTinus/simple-acme"
)

Install-Module -Name SignPath

Write-Host ""
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host "Signing $Path..."					  -ForegroundColor Green
Write-Host "------------------------------------" -ForegroundColor Green
Write-Host ""

$Parameters = @{ 
    productVersion = $Version
}
$ArtifactConfiguration = "initial"
if ($Path.EndsWith(".nupkg")) {
	$ArtifactConfiguration = "nuget"
}

Submit-SigningRequest `
    -InputArtifactPath $Path `
    -ProjectSlug "simple-acme" `
    -SigningPolicySlug "test-signing" `
	-ArtifactConfigurationSlug $ArtifactConfiguration `
    -WaitForCompletion `
	-Force `
	-Parameters $Parameters `
    -OutputArtifactPath $Path `
    -OrganizationId "e396b30d-0bbf-442f-b958-78da3e8c1b7e" `
    -ApiToken $ApiToken
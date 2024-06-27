param (
	[ValidatePattern("^\d+\.\d+.\d+.\d+")]
	[string]
	$ReleaseVersionNumber = "2.0.0.0",

	[string[]]
	$Releases = @("Release,ReleaseTrimmed"),

	[string[]]
	$Archs = @("win-x64", "win-x86", "win-arm64"),

	[switch]
	$BuildPlugins = $true,

	[switch]
	$BuildNuget = $true,

	[switch]
	$Clean = $true

	[switch]
	$CreateArtifacts = $true
)

cls
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
Push-Location $PSScriptFilePath.Directory
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot "build"

# Restore NuGet packages
& dotnet restore $RepoRoot\src\main\wacs.csproj

# Clean solution
if ($Clean) {
	foreach ($release in $releases) {
		foreach ($arch in $archs) {
			Write-Host "Clean $arch $release..."
			& dotnet clean $RepoRoot\src\main\wacs.csproj -c $release -r $arch /p:SelfContained=true
		}
	}
}


# Build Nuget package
if ($BuildNuget) {
	& dotnet pack $RepoRoot\src\main\wacs.csproj -c "Release" /p:PublishSingleFile=false /p:PublishReadyToRun=false
}

# Build regular releases
foreach ($release in $releases) 
{
	foreach ($arch in $archs) 
	{
		Write-Host "Publish $arch $release..."
		$extra = ""
		if ($release.EndsWith("Trimmed")) {
			$extra = "/p:warninglevel=0"
		}
		& dotnet publish $RepoRoot\src\main\wacs.csproj -c $release -r $arch --self-contained $extra
		if (-not $?)
		{
			Pop-Location
			throw "The dotnet publish process returned an error code."
		}
	}
}

# Build plugins
if ($BuildPlugins) {
	$plugins = @(
		"store.keyvault"
		"store.userstore"
		"validation.dns.acme"
		"validation.dns.aliyun"
		"validation.dns.azure"
		"validation.dns.cloudflare"
		"validation.dns.digitalocean"
		"validation.dns.dnsexit"
		"validation.dns.dnsmadeeasy"
		"validation.dns.domeneshop"
		"validation.dns.dreamhost"
		"validation.dns.godaddy"
		"validation.dns.googledns"
		"validation.dns.hetzner"
		"validation.dns.infomaniak"
		"validation.dns.linode"
		"validation.dns.luadns"
		"validation.dns.ns1"
		"validation.dns.rfc2136"
		"validation.dns.route53"
		"validation.dns.simply"
		"validation.dns.transip"
		"validation.dns.tencent"
		"validation.http.ftp"
		"validation.http.rest"
		"validation.http.sftp"
	)
	foreach ($plugin in $plugins) 
	{
		Write-Host "Publish $plugin..."
		& dotnet publish $RepoRoot\src\plugin.$plugin\wacs.$plugin.csproj -c "Release"
		if (-not $?)
		{
			Pop-Location
			throw "The dotnet publish process returned an error code."
		}
	}
}
if ($CreateArtifacts) 
{
	./create-artifacts.ps1 $RepoRoot $ReleaseVersionNumber -Releases $Releases -Archs $Archs -BuildPlugins:$BuildPlugins -BuildNuget:$BuildNuget
}
Pop-Location
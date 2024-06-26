﻿param (
	[Parameter(Mandatory=$true)]
	[string]
	$Root,

	[Parameter(Mandatory=$true)]
	[string]
	$Version,

	[string]
	$Password,

	[string[]]
	$Releases = @("Release,ReleaseTrimmed"),

	[string[]]
	$Archs = @("win-x64", "win-x86", "win-arm64"),

	[switch]
	$BuildPlugins = $true,

	[switch]
	$BuildNuget = $true
)

Add-Type -Assembly "system.io.compression.filesystem"
$Temp = "$Root\build\temp\"
$Out = "$Root\build\artifacts\"
if (Test-Path $Temp)
{
    Remove-Item $Temp -Recurse
}
New-Item $Temp -Type Directory

if (Test-Path $Out)
{
    Remove-Item $Out -Recurse
}
New-Item $Out -Type Directory

function PlatformRelease
{
	param($ReleaseType, $Platform)

	Remove-Item $Temp\* -recurse
	$Postfix = "pluggable"
	if ($ReleaseType -eq "ReleaseTrimmed") {
		$Postfix = "trimmed"
	}
	$MainZip = "simple-acme.v$Version.$Platform.$Postfix.zip"
	$MainZipPath = "$Out\$MainZip"
	$MainBinDir = "$Root\src\main\bin\$ReleaseType\net8.0\$Platform"
	if (!(Test-Path $MainBinDir))
	{
		$MainBinDir = "$Root\src\main\bin\Any CPU\$ReleaseType\net8.0\$Platform"
	}
	$MainBinFile = "wacs.exe"
	if ($Platform -like "linux*") {
		$MainBinFile = "wacs"
	}
	if (Test-Path $MainBinDir)
	{
		if ($Platform -like "win*") {
			./sign-exe.ps1 "$MainBinDir\publish\$MainBinFile" "$Root\build\codesigning.pfx" $Password
		}
		Copy-Item "$MainBinDir\publish\$MainBinFile" $Temp
		if ($Platform -like "linux*") {
			Copy-Item "$MainBinDir\settings.linux.json" "$Temp\settings_default.json"
		} else {
			Copy-Item "$MainBinDir\settings.json" "$Temp\settings_default.json"
		}
		Copy-Item "$Root\dist\*" $Temp -Recurse
		Set-Content -Path "$Temp\version.txt" -Value "v$Version ($Platform, $ReleaseType)"
		[io.compression.zipfile]::CreateFromDirectory($Temp, $MainZipPath)
	}

	# Managed debugger interface as optional extra download
	if ($Platform -like "win*") {
		$DbiZip = "mscordbi.v$Version.$Platform.zip"
		$DbiZipPath = "$Out\$DbiZip"
		if (!(Test-Path $DbiZipPath)) {
			CreateArtifact $MainBinDir @("mscordbi.dll") $DbiZipPath
		}
	}
}

function CreateArtifact {
	param($Dir, $Files, $Target)

	Remove-Item $Temp\* -recurse
	foreach ($file in $files) {
		Copy-Item "$Dir\$file" $Temp
	}
	[io.compression.zipfile]::CreateFromDirectory($Temp, $Target)
}

function PluginRelease
{
	param($Dir, $Files)

	Remove-Item $Temp\* -recurse
	$PlugZip = "$Dir.v$Version.zip"
	$PlugZipPath = "$Out\$PlugZip"
	$PlugBin = "$Root\src\$Dir\bin\Release\net8.0\publish"
	if (!(Test-Path $PlugBin))
	{
		$PlugBin = "$Root\src\$Dir\bin\Any CPU\Release\net8.0\publish"
	}
	CreateArtifact $PlugBin $Files $PlugZipPath

	# Special for the plugin
	if ($Dir -eq "plugin.validation.http.ftp") {
		$GnuTlsZip = "gnutls.v$Version.x64.zip"
		$GnuTlsZipPath = "$Out\$GnuTlsZip"
		$GnuTlsSrc = $PlugBin
		if (!(Test-Path $GnuTlsZipPath)) {
			CreateArtifact $GnuTlsSrc @(
				"libgcc_s_seh-1.dll",
				"libgmp-10.dll",
				"libgnutls-30.dll",
				"libhogweed-6.dll",
				"libnettle-8.dll",
				"libwinpthread-1.dll") $GnuTlsZipPath
		}
	}
}

function NugetRelease
{
	$PackageFolder = "$Root\src\main\nupkg"
	if (Test-Path $PackageFolder)
	{
		Copy-Item "$PackageFolder\*" $Out -Recurse
	}
}

if ($BuildNuget) {
	NugetRelease
}

foreach ($release in $releases) {
	foreach ($arch in $archs) {
		PlatformRelease $release $arch 
	}
}

if ($BuildPlugins) {
	PluginRelease plugin.store.keyvault @(
		"Azure.Core.dll",
		"Azure.Identity.dll",
		"Azure.ResourceManager.dll",
		"Azure.ResourceManager.KeyVault.dll",
		"Azure.Security.KeyVault.Certificates.dll",
		"Microsoft.Bcl.AsyncInterfaces.dll",
		"Microsoft.Identity.Client.dll",
		"Microsoft.Identity.Client.Extensions.Msal.dll",
		"Microsoft.IdentityModel.Abstractions.dll",
		"PKISharp.WACS.Plugins.Azure.Common.dll",
		"PKISharp.WACS.Plugins.StorePlugins.KeyVault.dll",
		"System.ClientModel.dll",
		"System.Memory.Data.dll"
	)
	PluginRelease plugin.store.userstore @(
		"PKISharp.WACS.Plugins.StorePlugins.UserStore.dll"
	)
	PluginRelease plugin.validation.dns.acme @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Acme.dll"
	)
	PluginRelease plugin.validation.dns.aliyun @(
		"AlibabaCloud.EndpointUtil.dll",
		"AlibabaCloud.GatewaySpi.dll",
		"AlibabaCloud.OpenApiClient.dll",
		"AlibabaCloud.OpenApiUtil.dll",
		"AlibabaCloud.SDK.Alidns20150109.dll",
		"AlibabaCloud.TeaUtil.dll",
		"AlibabaCloud.TeaXML.dll",
		"Aliyun.Credentials.dll",
		"Newtonsoft.Json.dll",
		"Tea.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.ALiYun.dll"
	)
	PluginRelease plugin.validation.dns.azure @(
		"Azure.Core.dll",
		"Azure.Identity.dll",
		"Azure.ResourceManager.dll",
		"Azure.ResourceManager.Dns.dll"
		"Microsoft.Bcl.AsyncInterfaces.dll",
		"Microsoft.Identity.Client.dll",
		"Microsoft.Identity.Client.Extensions.Msal.dll"
		"Microsoft.IdentityModel.Abstractions.dll"
		"PKISharp.WACS.Plugins.Azure.Common.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Azure.dll",
		"System.ClientModel.dll",
		"System.Memory.Data.dll"
	)
	PluginRelease plugin.validation.dns.cloudflare @(
		"FluentCloudflare.dll",
		"Newtonsoft.Json.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Cloudflare.dll"
	)
	PluginRelease plugin.validation.dns.digitalocean @(
		"DigitalOcean.API.dll",
		"RestSharp.dll",
		"Newtonsoft.Json.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.DigitalOcean.dll"
	)
	PluginRelease plugin.validation.dns.dnsexit @(
		"Newtonsoft.Json.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Dnsexit.dll"
	)
	PluginRelease plugin.validation.dns.dnsmadeeasy @(
		"PKISharp.WACS.Plugins.ValidationPlugins.DnsMadeEasy.dll",
		"Newtonsoft.Json.dll"
	)
	PluginRelease plugin.validation.dns.domeneshop @(
		"Abstractions.Integrations.Domeneshop.Models.dll",
		"Abstractions.Integrations.Domeneshop.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Domeneshop.dll"
	)
	PluginRelease plugin.validation.dns.dreamhost @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Dreamhost.dll"
	)
	PluginRelease plugin.validation.dns.godaddy @(
		"Newtonsoft.Json.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Godaddy.dll"
	)
	PluginRelease plugin.validation.dns.googledns @(
		"Google.Apis.dll",
		"Google.Apis.Auth.dll",
		"Google.Apis.Core.dll",
		"Google.Apis.Dns.v1.dll",
		"Newtonsoft.Json.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.GoogleDns.dll"
	)
	PluginRelease plugin.validation.dns.hetzner @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Hetzner.dll"
	)
	PluginRelease plugin.validation.dns.infomaniak @(
		"PKISharp.WACS.Plugins.ValidationPlugins.InfoManiak.dll",
		"Newtonsoft.Json.dll"
	)
	PluginRelease plugin.validation.dns.linode @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Linode.dll",
		"Newtonsoft.Json.dll"
	)
	PluginRelease plugin.validation.dns.luadns @(
		"PKISharp.WACS.Plugins.ValidationPlugins.LuaDns.dll"
	)
	PluginRelease plugin.validation.dns.ns1 @(
		"PKISharp.WACS.Plugins.ValidationPlugins.NS1.dll"
	)
	PluginRelease plugin.validation.dns.rfc2136 @(
		"ARSoft.Tools.Net.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Rfc2136.dll"
	)
	PluginRelease plugin.validation.dns.route53 @(
		"AWSSDK.Core.dll",
		"AWSSDK.Route53.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Route53.dll"
	)
	PluginRelease plugin.validation.dns.simply @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Simply.dll"
	)
	PluginRelease plugin.validation.dns.tencent @(
		"Newtonsoft.Json.dll",
		"TencentCloudCommon.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Tencent.dll"
	)
	PluginRelease plugin.validation.dns.transip @(
		"Newtonsoft.Json.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.TransIp.dll"
	)
	PluginRelease plugin.validation.http.ftp @(
		"FluentFTP.dll",
		"FluentFTP.GnuTLS.dll",
		"libgcc_s_seh-1.dll",
		"libgmp-10.dll",
		"libgnutls-30.dll",
		"libhogweed-6.dll",
		"libnettle-8.dll",
		"libwinpthread-1.dll",
		"PKISharp.WACS.Plugins.ValidationPlugins.Ftp.dll"
	)
	PluginRelease plugin.validation.http.rest @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Rest.dll"
	)
	PluginRelease plugin.validation.http.sftp @(
		"PKISharp.WACS.Plugins.ValidationPlugins.Sftp.dll",
		"Renci.SshNet.dll"
	)
	PluginRelease plugin.validation.http.webdav @(
		"PKISharp.WACS.Plugins.ValidationPlugins.WebDav.dll",
		"WebDav.Client.dll"
	)
}

"Created artifacts:"
dir $Out
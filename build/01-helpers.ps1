function BuildPath
{
	param($path)
	if (!(Test-Path $path))
	{
		# For some reason AppVeyor generates paths like this instead of the above on local systems
		$path = $path.Replace("\bin\", "\bin\Any CPU\")
	}
	return $path
}

function ClearFolder
{
	param($path)
	if (Test-Path $path)
	{
		Remove-Item $path -Recurse
	} else {
		Write-Host "$path not removed"
	}

}

function EnsureFolder
{
	param($path)
	if (-not (Test-Path $path))
	{
		New-Item $path -Type Directory | Out-Null
	}
}

function Get-FriendlySize {
    param($Bytes)
    $sizes='Bytes,KB,MB,GB,TB,PB,EB,ZB' -split ','
    for($i=0; ($Bytes -ge 1kb) -and
        ($i -lt $sizes.Count); $i++) {$Bytes/=1kb}
    $N=1; if($i -eq 0) {$N=0}
    "{0:N$($N)} {1}" -f $Bytes, $sizes[$i]
}

function Get-YamlValue {
	param($key, $yaml)
	if ($yaml -match "$($key): ([\S]+)") {
		if (![string]::IsNullOrWhiteSpace($Matches[1])) {
			Write-Host "$key $($Matches[1]) detected"
			return $Matches[1]
		} else {
			Write-Host "No $key detected 1"
			exit
		}
	} else {
		Write-Host "No $key detected 2"
		exit
	}
}

function Compress {
	param($Dir, $Target)
	Add-Type -Assembly "system.io.compression.filesystem"
	[io.compression.zipfile]::CreateFromDirectory($Dir, $Target)
}

function Decompress {
	param($Dir, $Target)
	Add-Type -Assembly "system.io.compression.filesystem"
	[io.compression.zipfile]::ExtractToDirectory($Target, $Dir)
}

function Status {
	param($Message)

	Write-Host ""
	Write-Host "------------------------------------"	-ForegroundColor Green
	Write-Host $Message									-ForegroundColor Green
	Write-Host "------------------------------------"	-ForegroundColor Green
	Write-Host ""
}

function ClearFolders {
	EnsureFolder $Temp
	EnsureFolder $Out
	EnsureFolder $Final
	EnsureFolder $Bundle
	Remove-Item $Temp\* -recurse
	Remove-Item $Out\* -recurse
	Remove-Item $Final\* -recurse
	Remove-Item $Bundle\* -recurse
}

$Version = $env:APPVEYOR_BUILD_VERSION
$Configs = $env:Configs.Split()
$Platforms = $env:Platforms.Split()
$NetVersion = $env:NetVersion
$BuildNuget = ($env:NuGet -eq "1")
$BuildPlugins = ($env:Plugins -eq "1")
$BuildPluginsCount = $env:PluginsCount
$Clean = ($env:Clean -eq "1")
$Root = $env:APPVEYOR_BUILD_FOLDER
$Temp = "$Root\out\temp\"
$Out = "$Root\out\unsigned\"
$Bundle = "$Root\out\bundle\"
$Final = "$Root\out\signed\"

try {
	cls
} catch {
	# Ignore
}
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
		New-Item $Out -Type Directory | Out-Null
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

function Compress {
	param($Dir, $Target)
	Add-Type -Assembly "system.io.compression.filesystem"
	[io.compression.zipfile]::CreateFromDirectory($Dir, $Target)
}

function Status {
	param($Message)

	Write-Host ""
	Write-Host "------------------------------------"	-ForegroundColor Green
	Write-Host $Message									-ForegroundColor Green
	Write-Host "------------------------------------"	-ForegroundColor Green
	Write-Host ""
}
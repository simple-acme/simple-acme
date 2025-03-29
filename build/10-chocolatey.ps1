$ChocoPath = "..\..\chocolatey"
Push-Location $ChocoPath

if ([string]::IsNullOrWhiteSpace($env:ChocoApiKey)) {
	$env:ChocoApiKey = Read-Host "Chocolatey API key"
}

$target = ".\simple-acme\bin\"
if (!(Test-Path $target)) {
	New-Item $target -Type Directory
}
Get-ChildItem $target | Remove-Item
$32bit = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x86.pluggable.zip"
$64bit = "$Final\simple-acme.v$env:APPVEYOR_BUILD_VERSION.win-x64.pluggable.zip"
Copy-Item $32bit -Destination $target
Copy-Item $64bit -Destination $target
$checksum32 = (Get-FileHash $32bit).Hash.ToLower()
$checksum64 = (Get-FileHash $64bit).Hash.ToLower()
$packageVersion = $($env:APPVEYOR_REPO_TAG_NAME.Replace("v", [string]::Empty))

function Replace-Stuff {
	param($path)
	$content = Get-Content -Path $path
	$content = $content -replace '\$tag(.+=.+)"(.+)"',"`$tag`$1`"$env:APPVEYOR_REPO_TAG_NAME`""
	$content = $content -replace '\$build(.+=.+)"(.+)"',"`$build`$1`"$env:APPVEYOR_BUILD_VERSION`""
	$content = $content -replace 'build:\(.+?\)',"build:(v$env:APPVEYOR_BUILD_VERSION)"
	$content = $content -replace 'commit:\(.+?\)',"commit:($env:APPVEYOR_REPO_COMMIT)"
	$content = $content -replace 'https:\/\/github\.com\/simple-acme\/simple-acme\/releases\/download\/(.+?)\/simple-acme\.[v\.0-9]+\.(.+?)\.(.+?).zip',"https://github.com/simple-acme/simple-acme/releases/download/$env:APPVEYOR_REPO_TAG_NAME/simple-acme.v$env:APPVEYOR_BUILD_VERSION.`$2.`$3.zip"
	$content = $content -replace 'checksum32: .+',"checksum32: $checksum32"
	$content = $content -replace 'checksum64: .+',"checksum64: $checksum64"
	$content = $content -replace '<version>.+</version>',"<version>$packageVersion</version>"
	Set-Content -Path $path -Value $content
}

function Replace-Stuff-Plugin {
	param($path, $plugin)
	$content = Get-Content -Path $path
	$content = $content -replace 'build:\(.+?\)',"build:(v$env:APPVEYOR_BUILD_VERSION)"
	$content = $content -replace 'commit:\(.+?\)',"commit:($env:APPVEYOR_REPO_COMMIT)"
	$content = $content -replace 'https:\/\/github\.com\/simple-acme\/simple-acme\/releases\/download\/(.+?).zip$',"https://github.com/simple-acme/simple-acme/releases/download/$env:APPVEYOR_REPO_TAG_NAME/$($plugin.artifact)"
	$content = $content -replace 'checksum: .+',"checksum: $($plugin.checksum)"
	$content = $content -replace '<version>.+</version>',"<version>$packageVersion</version>"
	$content = $content -replace '<id>.+</id>',"<id>$($plugin.packageId)</id>"
	$content = $content -replace '<packageSourceUrl>.+</packageSourceUrl>',"<packageSourceUrl>https://github.com/simple-acme/chocolatey/$($plugin.packageId)</packageSourceUrl>"
	$content = $content -replace '<title>.+</title>',"<title>simple-acme $($plugin.name) plugin</title>"
	$content = $content -replace '<description>.+</description>',"<description>$($plugin.name) $($plugin.typeHuman) plugin for simple-acme</description>"
	$content = $content -replace '<summary>.+</summary>',"<summary>$($plugin.name) $($plugin.typeHuman) plugin for simple-acme</summary>"
	$content = $content -replace '<docsUrl>.+</docsUrl>',"<docsUrl>https://simple-acme.com/reference/plugins/$($plugin.type.Replace(".","/"))/$($plugin.page)</docsUrl>"
	$content = $content -replace '<dependency id="simple-acme" version=".+" />',"<dependency id=`"simple-acme`" version=`"$packageVersion`" />"
	$content = $content -replace '\$artifact(.+=.+)"(.+)?"',"`$artifact`$1`"$($plugin.artifact)`""
	$tags = @("simple-acme", "plugin")
	$tags += $($plugin.type.Split(".")[0])
	if ($plugin.type.Contains(".")) {
		$tags += $($plugin.type.Split(".")[1])
	}
	$tags += $plugin.trigger
	if ($plugin.page -ne $plugin.trigger) {
		$tags += $plugin.page
	}
	if ($plugin.download -ne $plugin.trigger) {
		$tags += $plugin.download
	}	
	if ($plugin.provider -ne $null) {
		$tags += $plugin.provider.ToLower()
	}
	$content = $content -replace '<tags>.+</tags>',"<tags>$([string]::Join(" ", $tags))</tags>"
	Set-Content -Path $path -Value $content
}

function Parse-Yml {
	param($yml, $key, $default)
	$match = [regex]::Matches($yml, "$key`: (.+)")[0]
	if ($match.Success) {
		return $match.Groups[1].Value.Trim().Trim('"')
	}
	return $default
}

function Publish-Package {
	param($nuspec)
	Remove-Item "*.nupkg" 
	choco pack $nuspec
	choco push --source https://push.chocolatey.org/ --apikey $env:ChocoApiKey
}

# Update package metadata
git checkout main
git pull
git branch $env:APPVEYOR_REPO_TAG_NAME
git checkout $env:APPVEYOR_REPO_TAG_NAME
git pull

Replace-Stuff ".\simple-acme\tools\chocolateyinstall.ps1"
Replace-Stuff ".\simple-acme\tools\chocolateyuninstall.ps1"
Replace-Stuff ".\simple-acme\tools\VERIFICATION.txt"
Replace-Stuff ".\simple-acme\simple-acme.nuspec"
Publish-Package ".\simple-acme\simple-acme.nuspec"

$templateFolder = ".\simple-acme-plugin-template"
$pluginsYml = Get-Content "$Final\plugins.yml" -Raw
$matches = [regex]::Matches($pluginsYml,'\-[\S\s]+?[\n\r]{4,}')
foreach ($plugin in $matches) {
	if (!$plugin.Value.Contains("external: true")) {
		continue;
	}
	$data = @{
		type = Parse-Yml $plugin.Value "type"
		name = Parse-Yml $plugin.Value "name"
		trigger = Parse-Yml $plugin.Value "trigger"
		description = Parse-Yml $plugin.Value "description"
		provider = Parse-Yml $plugin.Value "provider"
	}
	$data.typeHuman = $data.type.`
		Replace("validation.dns", "validation").`
		Replace("validation.http", "validation")
	$data.page =  Parse-Yml $plugin.Value "page" $data.trigger
	$data.download =  Parse-Yml $plugin.Value "download" $data.trigger
	$data.artifact = "plugin.$($data.type).$($data.download).v$env:APPVEYOR_BUILD_VERSION.zip"
	$sourcePath = "$Final\$($data.artifact)"
	if (-not (Test-Path $sourcePath)) {
		Write-Host "Not found $($data.artifact)" -ForegroundColor Red
		continue;
	}	
	$data.checksum = (Get-FileHash $sourcePath).Hash.ToLower()
	$data.packageId = "simple-acme-$($data.type.Replace(".", "-"))-$($data.download)"

	Write-Host "Processing $($data.packageId)..."
	$packageFolder = ".\$($data.packageId)"
	$nuspecFile = "$packageFolder\$($data.packageId).nuspec" 
	if (!(Test-Path $packageFolder)) {
	 	New-Item $packageFolder -Type Directory
	}
	Get-Content "$templateFolder\nuspec.nuspec" | Set-Content $nuspecFile
	Replace-Stuff-Plugin $nuspecFile $data
	$toolsFolder = "$packageFolder\tools"
	if (!(Test-Path $toolsFolder)) {
		New-Item $toolsFolder -Type Directory
	}
	Get-Content "$templateFolder\tools\LICENSE.txt" | Set-Content "$toolsFolder\LICENSE.txt"
	Get-Content "$templateFolder\tools\VERIFICATION.txt" | Set-Content "$toolsFolder\VERIFICATION.txt"
	Get-Content "$templateFolder\tools\chocolateyinstall.ps1" | Set-Content "$toolsFolder\chocolateyinstall.ps1"
	Get-Content "$templateFolder\tools\chocolateyuninstall.ps1" | Set-Content "$toolsFolder\chocolateyuninstall.ps1"
	Replace-Stuff-Plugin "$toolsFolder\VERIFICATION.txt" $data
	Replace-Stuff-Plugin "$toolsFolder\chocolateyinstall.ps1" $data
	Replace-Stuff-Plugin "$toolsFolder\chocolateyuninstall.ps1" $data

	$binFolder = "$packageFolder\bin\"
	if (!(Test-Path $binFolder)) {
		New-Item $binFolder -Type Directory
	}
	Copy-Item $sourcePath $binFolder
	Publish-Package "$packageFolder\$($data.packageId).nuspec"
	break;
}

git add .
git commit -m "Release $env:APPVEYOR_REPO_TAG_NAME"
git push --set-upstream origin $env:APPVEYOR_REPO_TAG_NAME

Pop-Location
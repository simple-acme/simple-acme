$yaml = "downloads:
"
foreach ($artifact in Get-ChildItem $Final "*.zip") {
	$yaml += "
  - 
    name: $($artifact.Name)
    size: $(Get-FriendlySize $artifact.Length)
    sha256: $((Get-FileHash $artifact.FullName).Hash)"
}

$buildPath = "$($Final)build.yml"
Add-Content -Path $buildPath -Value $yaml

Status "Hashes straight from the press"
Get-Content -Path $buildPath
Status "Signed results ready!"
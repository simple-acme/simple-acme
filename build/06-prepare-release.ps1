$yaml = "releasename: $Version
releasetag: $Version
releasebuild: $Version
commit: $Commit
downloads: "

foreach ($artifact in Get-ChildItem $Out "*.zip") {
	$yaml += "
  - 
    name: $($artifact.Name)
    size: $(Get-FriendlySize $artifact.Length)
    sha256: $((Get-FileHash $artifact.FullName).Hash)"
}

Set-Content -Path "$($Final)build.yml" -Value $yaml

Status "Hashes straight from the press"
$yaml
Status "Signed results ready!"
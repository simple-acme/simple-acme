param (
	[Parameter(Mandatory=$true)]
	[string]
	$Root,

	[Parameter(Mandatory=$true)]
	[string]
	$Version
)

$Out = "$Root\out\artifacts\"
$yaml = "releasename: $Version
releasetag: $Version
releasebuild: $Version
downloads: "

foreach ($artifact in Get-ChildItem $Out) {
	$yaml += "
  - 
    name: $($artifact.Name)
    size: $(Get-FriendlySize $artifact.Length)
    sha256: $((Get-FileHash $artifact).Hash)"
}

Set-Content -Path "$($out)build.yml" -Value $yaml

Status "Hashes straight from the press"
$yaml
param (
	[Parameter(Mandatory=$true)]
	[string]
	$Root
)

$Out = "$Root\out"
Remove-Item "$Out\artifacts\signingbundle.zip" -Force
Remove-Item "$Out\plugins.xml" -Force
Remove-Item "$Out\temp" -Force
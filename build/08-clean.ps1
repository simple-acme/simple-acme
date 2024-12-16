$Out = "$Root\out"
Remove-Item "$Out\artifacts\signingbundle.zip" -Force
if ($artifacts -and $artifacts['signingbundle.zip']) {
	Write-Host $artifacts['signingbundle.zip'].path
}
if ($BuildPlugins) {
	Remove-Item "$Out\plugins.xml" -Force
}
Remove-Item "$Out\temp" -Force
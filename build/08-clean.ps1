$Out = "$Root\out"
Remove-Item "$Out\artifacts\signingbundle.zip" -Force
if ($BuildPlugins) {
	Remove-Item "$Out\plugins.xml" -Force
}
Remove-Item "$Out\temp" -Force
# Something, something, Github...
$tag = "v$version"
gh release create $tag --generate-notes --draft --prerelease --verify-tag
Get-ChildItem $Final | Where-Object { $_.Extension -eq ".zip" } | Foreach-Object {
   gh release upload $tag $_.FullName --clobber
}
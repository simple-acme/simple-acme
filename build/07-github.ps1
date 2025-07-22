# Create GitHub release for pre-existing tag
# upload only .zip assets, .yml files and such
# are only needed for documentation website
gh release create $env:APPVEYOR_REPO_TAG_NAME --generate-notes --draft --prerelease --verify-tag
Get-ChildItem $Final | Where-Object { $_.Extension -eq ".zip" } | Foreach-Object {
   gh release upload $env:APPVEYOR_REPO_TAG_NAME $_.FullName --clobber
}
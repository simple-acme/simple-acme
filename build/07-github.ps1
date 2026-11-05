# Create GitHub release for pre-existing tag
# upload only .zip assets, .yml files and such
# are only needed for documentation website
gh release create $env:APPVEYOR_REPO_TAG_NAME --draft --prerelease --verify-tag --repo "simple-acme/simple-acme"
Get-ChildItem $Final | Where-Object { ($_.Extension -eq ".zip") -and (-not ($_.Name -like "win-acme*")) } | Foreach-Object {
   gh release upload $env:APPVEYOR_REPO_TAG_NAME $_.FullName --clobber --repo "simple-acme/simple-acme"
}

gh release create $env:APPVEYOR_REPO_TAG_NAME --draft --prerelease --repo "win-acme/win-acme"
Get-ChildItem $Final | Where-Object { ($_.Extension -eq ".zip") -and (-not ($_.Name -like "simple-acme*")) } | Foreach-Object {
   gh release upload $env:APPVEYOR_REPO_TAG_NAME $_.FullName --clobber --repo "win-acme/win-acme"
}
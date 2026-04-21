# Publish docs
$Files = Get-ChildItem $Final
$DocsPath = "..\..\simple-acme.github.io"
Push-Location $DocsPath
git checkout main
git pull
git branch $env:APPVEYOR_REPO_TAG_NAME
git checkout $env:APPVEYOR_REPO_TAG_NAME
git pull
$Files | Where-Object { $_.Extension -eq ".yml" } | Foreach-Object {
	Copy-Item $_ -Destination ".\_data\"
}
git add .
git commit -m "Release $env:APPVEYOR_REPO_TAG_NAME"
git push --set-upstream origin $env:APPVEYOR_REPO_TAG_NAME
Pop-Location
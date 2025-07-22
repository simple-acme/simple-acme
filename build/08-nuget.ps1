# Publish NuGet

if ([string]::IsNullOrWhiteSpace($env:NugetApiKey)) {
	$env:NugetApiKey = Read-Host "Nuget API key"
}

Get-ChildItem $Final | Where-Object { $_.Extension -eq ".nupkg" } | Foreach-Object {
   dotnet nuget push $_.FullName --source https://api.nuget.org/v3/index.json --skip-duplicate --api-key $env:NugetApiKey
}
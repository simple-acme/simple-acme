$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
& dotnet pack $RepoRoot\src\main\wacs.csproj -c "Release"
& dotnet tool uninstall simple-acme --global
& dotnet tool install --global --add-source $RepoRoot\src\main\nupkg\ simple-acme --version 2.1.0

function Invoke-TestDistHasNoCaseOnlyDuplicatePaths {
    param([scriptblock]$Assert)

    & $Assert "dist contains no case-only duplicate paths" {
        $distPath = Join-Path $PSScriptRoot '..\dist'
        $files = Get-ChildItem -Path $distPath -File -Recurse
        $groups = @($files | Group-Object { $_.FullName.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 })
        if ($groups.Count -gt 0) {
            $dupes = $groups | ForEach-Object { $_.Group.FullName -join ', ' } | Out-String
            throw "Found case-only duplicate path(s) in dist: $dupes"
        }
    }
}

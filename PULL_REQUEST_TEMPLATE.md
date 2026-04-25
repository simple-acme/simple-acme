## Summary
- Describe what changed and why.

## Validation
- [ ] `dotnet build src/wacs.slnx -c Release` (or docs-only change)
- [ ] `powershell.exe -ExecutionPolicy Bypass -File tests\Run-Tests.ps1`
- [ ] Reviewed `PASS/SKIP/FAIL` output and explained skips if relevant

## Documentation impact
- [ ] Updated `README.md` for runtime/setup/config changes
- [ ] Updated `CONTRIBUTING.md` for contributor workflow/contract changes
- [ ] Updated architecture docs for assumption changes

## Time-sensitive checks (if applicable)
- Verification date (UTC):
- Commands run for fork/upstream comparison:
  - `git fetch origin`
  - `git fetch upstream`
  - `git log --oneline --left-right upstream/main...origin/main`

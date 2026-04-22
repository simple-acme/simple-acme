# Contributing

## Prerequisites
- Windows PowerShell 5.1 only for orchestrator scripts/modules.
- No external dependencies for the orchestrator path (no Node.js, npm, TypeScript, Pester, NuGet modules).

## Local testing
- Run the zero-dependency test runner:
  - `powershell.exe -ExecutionPolicy Bypass -File tests\Run-Tests.ps1`
- Run static compatibility checks:
  - `Select-String -Recurse -Pattern 'SkipCertificateCheck|\$using:|ConvertFrom-Json.*-Depth|\?\?|\?\.' -Include '*.ps1','*.psm1' .`

## Connector contract
Each connector module must:
- Start with `#Requires -Version 5.1` and `$ErrorActionPreference = 'Stop'`.
- Export six functions named exactly `Invoke-<Type>ConnectorProbe/Deploy/Bind/Activate/Verify/Rollback`.
- Read secrets via environment variables only.
- Implement rollback using `previous_artifact_ref`; if unavailable, log a warning and return.

## PR checklist
- [ ] Tests pass via `tests\Run-Tests.ps1`.
- [ ] PowerShell 5.1 compatibility maintained.
- [ ] Atomic writes are used (`.tmp` + `Move-Item -Force`).
- [ ] No `Write-Host` in modules (use `Write-CertificaatLog`).
- [ ] Documentation updated for behavior/config changes.

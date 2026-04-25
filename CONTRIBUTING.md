# Contributing

This repository contains two main contribution surfaces:
- .NET `simple-acme` application code (`src/`)
- PowerShell orchestrator/connector code (`*.ps1`, `*.psm1`)

Use this guide for local build/test, connector contributions, and doc hygiene.

---

## Prerequisites

- Windows PowerShell 5.1 for orchestrator and connector runtime behavior.
- .NET SDK 10.x for compiling `src/wacs.slnx`.
- Optional: `pwsh` for `build/compile-local.ps1` SDK bootstrap.

---

## Build and test

### Compile

```bash
dotnet restore src/wacs.slnx
dotnet build src/wacs.slnx -c Release
```

Or auto-bootstrap SDK when missing:

```powershell
pwsh -NoLogo -NoProfile -File build/compile-local.ps1
```

### Test runner

```powershell
powershell.exe -ExecutionPolicy Bypass -File tests\Run-Tests.ps1
```

Interpretation rules:
- `[PASS]`: assertion passed.
- `[SKIP]`: environment-dependent skip (for example no `dotnet` or no Pester function definitions).
- `[FAIL]`: test failed.
- Any failure causes non-zero exit.

Optional static compatibility sweep:

```powershell
Select-String -Recurse -Pattern 'SkipCertificateCheck|\$using:|ConvertFrom-Json.*-Depth|\?\?|\?\.' -Include '*.ps1','*.psm1' .
```

---

## Connector contribution contract

### Runtime expectations

- Accept a single parameter: `param([hashtable]$Context)`.
- Provide six lifecycle operations: `Probe`, `Deploy`, `Bind`, `Activate`, `Verify`, `Rollback`.
- Rollback should use `previous_artifact_ref` when available.
- Throw terminating errors on unrecoverable failures.

### Export naming reality in this repository

Current connectors use one of these export styles:
1. **Dual export style** (most Windows-native connectors):
   - legacy lifecycle names: `Invoke-<Type>Probe/Deploy/Bind/Activate/Verify/Rollback`
   - connector aliases: `Invoke-<Type>ConnectorProbe/Deploy/Bind/Activate/Verify/Rollback`
2. **Connector-only export style** (some REST connectors):
   - `Invoke-<Type>ConnectorProbe/Deploy/Bind/Activate/Verify/Rollback`

When adding a new connector, follow the dominant pattern used by neighboring connector files in `connectors/` for compatibility.

### Canonical connector template

```powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-ExampleProbe { param([hashtable]$Context)
    @{ reachable = $true; auth_valid = $true; detail = 'Probe ok.' }
}

function Invoke-ExampleDeploy { param([hashtable]$Context)
    @{ artifact_ref = [string]$Context.event.thumbprint; detail = 'Deploy ok.' }
}

function Invoke-ExampleBind { param([hashtable]$Context)
    @{ success = $true; detail = 'Bind ok.' }
}

function Invoke-ExampleActivate { param([hashtable]$Context)
    @{ success = $true; detail = 'Activate ok.' }
}

function Invoke-ExampleVerify { param([hashtable]$Context)
    @{ verified = $true; detail = 'Verify ok.' }
}

function Invoke-ExampleRollback { param([hashtable]$Context)
    if ([string]::IsNullOrWhiteSpace([string]$Context.previous_artifact_ref)) {
        throw 'No previous_artifact_ref set for rollback.'
    }
    @{ success = $true; detail = 'Rollback ok.' }
}

function Invoke-ExampleConnectorProbe { param([hashtable]$Context) Invoke-ExampleProbe -Context $Context }
function Invoke-ExampleConnectorDeploy { param([hashtable]$Context) Invoke-ExampleDeploy -Context $Context }
function Invoke-ExampleConnectorBind { param([hashtable]$Context) Invoke-ExampleBind -Context $Context }
function Invoke-ExampleConnectorActivate { param([hashtable]$Context) Invoke-ExampleActivate -Context $Context }
function Invoke-ExampleConnectorVerify { param([hashtable]$Context) Invoke-ExampleVerify -Context $Context }
function Invoke-ExampleConnectorRollback { param([hashtable]$Context) Invoke-ExampleRollback -Context $Context }

Export-ModuleMember -Function \
    Invoke-ExampleProbe,Invoke-ExampleDeploy,Invoke-ExampleBind,Invoke-ExampleActivate,Invoke-ExampleVerify,Invoke-ExampleRollback,\
    Invoke-ExampleConnectorProbe,Invoke-ExampleConnectorDeploy,Invoke-ExampleConnectorBind,Invoke-ExampleConnectorActivate,Invoke-ExampleConnectorVerify,Invoke-ExampleConnectorRollback
```

---

## Documentation requirements for PRs

If your change affects behavior, update docs in the same PR:
- `README.md` for operator/runtime behavior
- `CONTRIBUTING.md` for dev workflow and contracts
- `docs/hybrid-architecture-phase1.md` for architecture-level assumptions

For time-sensitive statements (fork status, feature parity, dated comparisons), include:
- explicit verification date
- reproducible command(s) used for verification

---

## PR checklist

- [ ] Build succeeds (`dotnet build src/wacs.slnx -c Release`) or change is docs-only.
- [ ] `tests\Run-Tests.ps1` run and results reviewed (`PASS/SKIP/FAIL`).
- [ ] PowerShell 5.1 compatibility preserved for orchestrator/connector scripts.
- [ ] Connector exports follow repository naming pattern.
- [ ] Docs updated for behavior/configuration changes.

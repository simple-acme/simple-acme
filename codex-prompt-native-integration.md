# Codex Task Prompt: Native simple-acme Integration into Certificate Orchestrator

## Objective
Implement a native integration between simple-acme and the Certificate orchestrator by introducing a Script-plugin bridge, adding Windows-native connector implementations, updating setup schemas/menu metadata, and documenting the operational model.

This task supersedes prior assumptions that broad connector coverage required external runtimes. The repository already contains native PowerShell deployment primitives in `dist/Scripts/` and these should be reused at the connector level (inline cmdlet/API logic, no child-process wrappers for the new connectors).

---

## Ground Rules

- Target runtime: **Windows PowerShell 5.1** compatible code.
- For all new PowerShell modules/scripts: set `$ErrorActionPreference = 'Stop'`.
- New connector implementations must be **native/lolbin-first** and avoid external modules unless explicitly categorized as external dependency stubs.
- Rollback must use `previous_artifact_ref` (old thumbprint) and re-apply it through the exact same binding path.
- Exchange connector must use local Exchange endpoint:
  - `New-PSSession -ConfigurationName Microsoft.Exchange -ConnectionUri http://localhost/PowerShell/`
  - Treat this as local Exchange management flow (not remote target orchestration).
- `dist/Scripts/New-CertificateDropFile.ps1` must fail gracefully when `CERTIFICATE_DROP_DIR` is missing:
  - write an event via `Write-EventLog`
  - exit with code `1`.

---

## Existing Capability Baseline (do not re-invent)

The repo already includes deployment scripts under `dist/Scripts/` for ADFS, Exchange, RDS, NTDS, SSTP, WinRM, SQL Server, Windows Admin Center, and others. The integration work should align with those native cmdlets/APIs.

The simple-acme Script Installation Plugin token set is available for bridge wiring:

- `{RenewalId}`
- `{CertThumbprint}`
- `{OldCertThumbprint}`
- `{CacheFile}`
- `{CacheFolder}`
- `{CachePassword}`
- `{StorePath}`
- `{StoreType}`
- `{CertCommonName}`
- `{CertFriendlyName}`

---

## Required Changes

### Step 1 — Create simple-acme → Certificate bridge script

Create new file:

- `dist/Scripts/New-CertificateDropFile.ps1`

This script is invoked from simple-acme Script Installation Plugin with:

```text
--script "dist\Scripts\New-CertificateDropFile.ps1"
--scriptparameters "'<POLICY-ID>' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"
```

> Replace `<POLICY-ID>` with the literal policy_id from policies.json (e.g. `'prod-rdgw'`).
> This value is the first positional argument to New-CertificateDropFile.ps1.

The script writes a JSON drop file to `$env:CERTIFICATE_DROP_DIR`:

```json
{
  "renewal_id": "...",
  "common_name": "...",
  "thumbprint": "...",
  "old_thumbprint": "...",
  "cache_file": "...",
  "cache_password": "...",
  "store_path": "...",
  "store_type": "CertificateStore|PfxFile|PemFiles|CentralSsl",
  "timestamp": "ISO8601"
}
```

Behavioral requirements:

- Validate required parameters.
- Validate `CERTIFICATE_DROP_DIR` exists and is writable.
- On missing env var/path: log to Windows Event Log (`Write-EventLog`) and `exit 1`.
- Use a deterministic, collision-safe filename strategy (e.g., renewal id + timestamp + random suffix).

---

### Step 2 — Add 10 native connectors (Category A + C)

Create these modules:

- `connectors/adfs.psm1`
- `connectors/rdp-listener.psm1`
- `connectors/rd-gateway.psm1`
- `connectors/rds-full.psm1`
- `connectors/ntds.psm1`
- `connectors/sstp.psm1`
- `connectors/winrm.psm1`
- `connectors/sql-server.psm1`
- `connectors/windows-admin-center.psm1`
- `connectors/exchange.psm1`

Implementation pattern:

- Inline the logic from corresponding scripts in `dist/Scripts/` using equivalent cmdlets/APIs.
- Do **not** shell out to those scripts as subprocess wrappers.
- Preserve operational behavior of native scripts.

Mapping guidance:

| Connector | Reference script | APIs/cmdlets |
|---|---|---|
| `adfs.psm1` | `ImportADFS.ps1` | `Set-AdfsCertificate`, `Set-AdfsSslCertificate`, `X509Store` |
| `rdp-listener.psm1` | `ImportRDListener.ps1` | `Get-CimInstance Win32_TSGeneralSetting`, `Set-CimInstance` |
| `rd-gateway.psm1` | `ImportRDGateway.ps1` | `Set-Item RDS:\GatewayServer\SSLCertificate\Thumbprint` |
| `rds-full.psm1` | `certificate-rdsgw.ps1` | `Set-RDCertificate`, `Import-PfxCertificate` |
| `ntds.psm1` | `ImportNTDS.ps1` | registry operations under `HKLM:\SOFTWARE\Microsoft\Cryptography\Services\NTDS` |
| `sstp.psm1` | `ImportSSTP.ps1` | `Set-RemoteAccess -SslCertificate`, service restart |
| `winrm.psm1` | `ImportWinRM.v2.ps1` | `New-WSManInstance`/`Set-WSManInstance`, `Restart-Service WinRM` |
| `sql-server.psm1` | `ImportSQL.ps1` | SQL cert registry binding + service restart |
| `windows-admin-center.psm1` | `ImportWindowsAdminCenter.ps1` | `Get-WmiObject Win32_Product`, `msiexec /i <GUID> /qn SME_THUMBPRINT=<thumb>` |
| `exchange.psm1` | `ImportExchange.v2.ps1` | local Exchange PSSession + `Enable-ExchangeCertificate` |

Each connector must expose the six-function contract:

- `Invoke-<Name>ConnectorProbe`
- `Invoke-<Name>ConnectorDeploy`
- `Invoke-<Name>ConnectorBind`
- `Invoke-<Name>ConnectorActivate`
- `Invoke-<Name>ConnectorVerify`
- `Invoke-<Name>ConnectorRollback`

Rollback requirement:

- Re-apply `previous_artifact_ref` (old thumbprint) through same binding path.

---

### Step 3 — Update device schemas

Modify:

- `setup/Device-Schemas.ps1`

Add schema entries for the 10 new connectors using native field types only (`string`, `secret`, `choice`).

Field expectations:

- `adfs`: no config fields
- `rdp_listener`: no config fields
- `rd_gateway`: no config fields
- `rds_full`: optional `rdcb_fqdn`
- `ntds`: no config fields
- `sstp`: optional `recreate_default_bindings` (`choice` as bool-style)
- `winrm`: no config fields
- `sql_server`: `instance_name` (`string`, default `MSSQLSERVER`)
- `windows_admin_center`: no config fields
- `exchange`: optional `services` (comma-separated list like `SMTP,IIS`)

Also add Category D connectors as **disabled stubs** with a `Requires` note field so UI can mark unavailable:

- `java_keystore`
- `kemp_module`
- `vbr_cloud_gateway`
- `azure_ad_app_proxy`
- `azure_application_gateway`
- `sparx_procloud`

---

### Step 4 — Update menu tree

Modify:

- `setup/Menu-Tree.ps1`

Reorganize to:

```text
Main Menu
  ├── Local Windows services
  │     ├── IIS
  │     ├── RDS Full Stack
  │     ├── RD Gateway
  │     ├── RDP Listener
  │     ├── ADFS
  │     ├── WinRM
  │     ├── SQL Server
  │     ├── NTDS (AD LDAPS)
  │     ├── SSTP VPN
  │     └── Windows Admin Center
  ├── Exchange
  │     ├── Exchange (local)
  │     └── Exchange Hybrid
  ├── Network appliances
  │     ├── F5 BIG-IP
  │     ├── Citrix ADC
  │     └── Kemp LoadMaster
  ├── External dependencies (read-only info)
  │     ├── Java KeyStore (requires JDK)
  │     ├── Veeam VBR (requires VBR module)
  │     ├── Azure App Gateway (requires AzureRM)
  │     └── Azure AD App Proxy (requires AzureAD)
  ├── Deployment policies
  └── Backup / Restore
```

---

### Step 5 — Update documentation

Modify:

- `README.md`

Add section: **Integration with simple-acme**

Include:

1. How to configure Script Installation Plugin to call `New-CertificateDropFile.ps1`
2. Exact `--script` and `--scriptparameters` example
3. Supported Store plugins (`PfxFile`, `CertificateStore`, `PemFiles`, `CentralSsl`) and connector compatibility by store type
4. Mapping from `{StorePath}`/`{StoreType}` tokens to connector input model

Add section: **Connector reference**

- Table of all connectors
- Category (`A`, `B`, `C`, `D`)
- Required Windows features/roles/dependencies
- Rollback support status

---

### Step 6 — Update previous prompt artifact

Modify:

- `codex-prompt.md`

Append clarification to Step 4 (stub connectors):

- The 8 previously stubbed device types in `setup/Device-Schemas.ps1` are now fully implemented by Step 2 of this native integration task.
- Stub-only guidance for those device types is superseded.

---

## File Change List

Create:

- `dist/Scripts/New-CertificateDropFile.ps1`
- `connectors/adfs.psm1`
- `connectors/rdp-listener.psm1`
- `connectors/rd-gateway.psm1`
- `connectors/rds-full.psm1`
- `connectors/ntds.psm1`
- `connectors/sstp.psm1`
- `connectors/winrm.psm1`
- `connectors/sql-server.psm1`
- `connectors/windows-admin-center.psm1`
- `connectors/exchange.psm1`
- `codex-prompt-native-integration.md` (this prompt)

Modify:

- `setup/Device-Schemas.ps1`
- `setup/Menu-Tree.ps1`
- `README.md`
- `codex-prompt.md`

---

## Verification Checklist

1. Configure simple-acme with Script installation and run renewal; confirm drop file appears in `$env:CERTIFICATE_DROP_DIR`.
2. Run orchestrator and confirm it consumes drop file and routes by `renewal_id`.
3. For each Category A connector, execute `Invoke-<Name>ConnectorDeploy` with a test cert and confirm binding updates.
4. Execute `Invoke-<Name>ConnectorRollback` and confirm prior thumbprint is rebound.
5. Validate schema and menu entries render correctly in setup TUI.
6. Confirm README content matches actual connector inventory and token mappings.

---

## Non-goals

- Do not introduce Node.js dependencies.
- Do not require PowerShell 7 for Category A/C connectors.
- Do not convert external dependency connectors (Category D) into full implementations in this task.

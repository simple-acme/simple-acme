# Certificaat Hybrid Certificate Lifecycle Orchestrator

## Prerequisites
- Windows Server 2019 or newer
- PowerShell 5.1 minimum (target runtime)
- No external modules or third-party binaries required

## Building wacs via CLI (.NET 10)

Prerequisite: install .NET SDK 10.x.

```bash
dotnet --info
dotnet restore src/wacs.slnx
dotnet build src/wacs.slnx -c Release
```

Visual Studio 2022 (17.x) does not support `net10.0` targeting. Use CLI builds with .NET 10 SDK, or use a newer Visual Studio version that supports .NET 10.

For automatic SDK bootstrapping, run:

```powershell
pwsh -NoLogo -NoProfile -File build/compile-local.ps1
```

## First-run Event Log source registration
Run once as Administrator:

```powershell
New-EventLog -LogName Application -Source Certificaat
```

## Environment variables

| Name | Required | Default | Description |
|---|---|---|---|
| CERTIFICAAT_DROP_DIR | Yes | `C:\certificaat\drop` | Watched folder where renewal JSON files are dropped |
| CERTIFICAAT_STATE_DIR | Yes | `C:\certificaat\state` | Job state JSON folder |
| CERTIFICAAT_LOG_DIR | Yes | `C:\certificaat\logs` | File log directory (optional output when folder exists) |
| CERTIFICAAT_VERIFY_MAX_ATTEMPTS | No | `3` | Retry attempts for verify style operations |
| CERTIFICAAT_ACTIVATE_TIMEOUT_MS | No | `120000` | Activation timeout hint in milliseconds |
| CERTIFICAAT_DEFAULT_FANOUT | No | `fail-fast` | Default policy mode when policy does not set one |
| CERTIFICAAT_SKIP_TLS_CHECK | No | unset | Set to `1` to disable TLS cert validation for connector API calls |
| CERTIFICAAT_RETRY_MAX_ATTEMPTS | No | `3` | Global retry attempts for connector operations |
| CERTIFICAAT_RETRY_BACKOFF_MS | No | `1000` | Initial retry backoff in milliseconds |
| CERTIFICAAT_HTTP_ENABLED | No | `0` | Set to `1` to accept events via native HttpListener API |
| CERTIFICAAT_HTTP_HOST | No | `127.0.0.1` | HTTP bind host for listener mode |
| CERTIFICAAT_HTTP_PORT | No | `8088` | HTTP bind port for listener mode |
| CERTIFICAAT_HTTP_BEARER_TOKEN | Conditional | unset | Required when `CERTIFICAAT_HTTP_ENABLED=1` |

## Run as scheduled task

```cmd
schtasks /Create /SC MINUTE /MO 5 /TN "Certificaat-Orchestrator" /TR "powershell.exe -ExecutionPolicy Bypass -File C:\certificaat\certificaat-orchestrator.ps1" /RU "SYSTEM"
```

## Add a new connector
1. Add a module under `connectors/`.
2. Export exactly six functions:
   - `Invoke-{ConnectorType}Probe`
   - `Invoke-{ConnectorType}Deploy`
   - `Invoke-{ConnectorType}Bind`
   - `Invoke-{ConnectorType}Activate`
   - `Invoke-{ConnectorType}Verify`
   - `Invoke-{ConnectorType}Rollback`
3. Ensure each function accepts `-Context` and throws terminating errors on failures.

## Run tests

```powershell
powershell.exe -File tests\Run-Tests.ps1
```

## Drop directory file format
Each file is named `{renewal_id}_{timestamp}.json` and should contain:

```json
{
  "event": "certificate.renewed",
  "renewal_id": "renewal-123",
  "deployment_policy_id": "prod-edge",
  "domain": "example.com",
  "cert_path": "C:\\certificaat\\out\\cert.pem",
  "key_path": "C:\\certificaat\\out\\key.pem",
  "fullchain_path": "C:\\certificaat\\out\\fullchain.pem",
  "thumbprint": "ABC123",
  "issuer": "Let's Encrypt",
  "not_before": "2026-04-21T00:00:00Z",
  "not_after": "2026-07-20T23:59:59Z"
}
```

## First-time setup

1. Run: `.\certificaat-setup.ps1`
2. Navigate to `ACME settings` — enter KID, HMAC secret, directory URL, domains.
3. Navigate to local/remote device categories — configure each endpoint.
4. Navigate to `Deployment policies` — assign devices to policies.
5. Navigate to `Backup & restore` > `Create backup` — store the backup file off-machine.
6. Exit setup. The .env and device configs are ready for the orchestrator.

## Backup and restore

```powershell
# Create a backup (prompts for passphrase)
.\certificaat-backup.ps1 -OutputPath C:\secure\certificaat-2026-04-21.certbak

# Restore on a new machine (prompts for passphrase)
.\certificaat-restore.ps1 -BackupPath \\fileserver\backups\certificaat-2026-04-21.certbak

# Verify a backup is readable without restoring
.\certificaat-restore.ps1 -BackupPath .\certificaat-2026-04-21.certbak -DryRun

# Scheduled backup via Task Scheduler (passphrase from environment — see security note below)
schtasks /create /tn "Certificaat backup" /sc WEEKLY /d MON /st 02:00 ^
  /tr "powershell.exe -NonInteractive -File C:\certificaat\certificaat-backup.ps1 ^
       -OutputPath \\fileserver\backups\certificaat-%DATE%.certbak ^
       -Passphrase (ConvertTo-SecureString $env:CERTIFICAAT_BACKUP_PASSPHRASE -AsPlainText -Force)"
```

Security note on unattended backup: store the passphrase in `CERTIFICAAT_BACKUP_PASSPHRASE` as a protected environment variable on the service account, not in a script file.

## Machine identity change recovery

```text
Symptoms: orchestrator logs "If the machine identity has changed, restore from backup"
Cause:    DPAPI-encrypted secrets are unreadable after OS reinstall, domain rejoin, or
          TPM/SID change.
Recovery:
  1. Install PowerShell and certificaat on the new machine.
  2. Run: .\certificaat-restore.ps1 -BackupPath <path-to-backup>
  3. Secrets are re-encrypted for the new machine identity automatically.
  4. Resume normal operation.
```


## Integration with simple-acme

Configure simple-acme Script Installation Plugin to write Certificaat drop files after each successful renewal.

```text
--installation Script
--script "dist\Scripts\New-CertificaatDropFile.ps1"
--scriptparameters "'<POLICY-ID>' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"
```


### Idempotent installer/update reconcile

Use the reconciler script to safely converge `.env` with existing simple-acme renewal state without manual JSON edits:

```powershell
.\certificaat-simple-acme-reconcile.ps1
```

Behavior:
- `create` when no matching renewal exists.
- `no-op` when `.env` already matches renewal JSON.
- `update` when drift is detected (runs `wacs --cancel --friendlyname <primary-domain>` then re-issues with full arguments).
- Merges `%PROGRAMDATA%\simple-acme\settings.json` to enforce:
  - `ScheduledTask.RenewalDays = 199`
  - `ScheduledTask.RenewalMinimumValidDays = 16`

### Store plugin compatibility

| simple-acme StoreType | Typical StorePath value | Notes |
|---|---|---|
| `PfxFile` | full path to `.pfx` | Required by connectors that need direct PFX import workflows. |
| `CertificateStore` | certificate store location | Preferred for local Windows service connectors using thumbprint bindings. |
| `PemFiles` | folder containing PEM artifacts | Used by file-based/reverse-proxy connectors. |
| `CentralSsl` | CCS directory path | Useful when post-processing from centralized file drops is required. |

### Store token mapping to connector input

- `{StoreType}` maps to `event.store_type` and determines expected artifact shape.
- `{StorePath}` maps to `event.store_path` and should be passed through unchanged.
- `{CertThumbprint}` maps to `event.thumbprint` for thumbprint-first native Windows connectors.
- `{OldCertThumbprint}` maps to `previous_artifact_ref` for rollback operations.
- `<POLICY-ID>` is a literal string matching a `policy_id` in `policies.json`. Replace it with the actual policy ID for this renewal.

## Connector reference

| Connector | Category | Requirements | Rollback |
|---|---|---|---|
| `iis` | A (Windows-native) | IIS role | Yes |
| `adfs` | A (Windows-native) | ADFS role | Yes |
| `rdp_listener` | A (Windows-native) | Remote Desktop Services | Yes |
| `rd_gateway` | A (Windows-native) | RD Gateway role | Yes |
| `rds_full` | A (Windows-native) | RDS deployment cmdlets | Yes |
| `ntds` | A (Windows-native) | Active Directory Domain Services | Yes |
| `sstp` | A (Windows-native) | RemoteAccess role + optional IIS binding support | Yes |
| `winrm` | A (Windows-native) | WinRM service | Yes |
| `sql_server` | A (Windows-native) | SQL Server installed locally | Yes |
| `windows_admin_center` | A (Windows-native) | Windows Admin Center installed | Yes |
| `exchange` | C (local PSSession) | Exchange Management endpoint on localhost | Yes |
| `f5_bigip` | B (REST) | F5 iControl REST access | Yes |
| `citrix_adc` | B (REST) | Citrix NITRO API access | Yes |
| `kemp` | B (REST) | Kemp REST/XML endpoint access | Yes |
| `java_keystore` | D (external dependency) | JDK / `keytool.exe` | Stub/disabled |
| `kemp_module` | D (external dependency) | Kemp PowerShell module | Stub/disabled |
| `vbr_cloud_gateway` | D (external dependency) | Veeam VBR module | Stub/disabled |
| `azure_application_gateway` | D (external dependency) | AzureRM module | Stub/disabled |
| `azure_ad_app_proxy` | D (external dependency) | AzureAD module | Stub/disabled |
| `sparx_procloud` | D (external dependency) | PowerShell 7 + external tooling | Stub/disabled |

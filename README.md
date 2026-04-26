# Certificate Hybrid Certificate Lifecycle Orchestrator

Certificate adds policy-based connector orchestration around `simple-acme` certificate issuance.

- `simple-acme` obtains/renews certificates.
- A script bridge drops certificate event JSON files.
- `certificate-orchestrator.ps1` fans out connector steps (`probe -> deploy -> bind -> activate -> verify`, with rollback support).

---

## Prerequisites

### Runtime (orchestrator)
- Windows Server 2019 or newer.
- Windows PowerShell 5.1.
- Required Windows roles/features depend on configured connectors (IIS, ADFS, RD Gateway, etc.).

### Build/development
- .NET SDK 10.x for compiling `src/wacs.slnx`.
- `pwsh` optional (used by `build/compile-local.ps1` to bootstrap SDK when missing).
- Internet access optional unless SDK bootstrap is needed.

---

## Build and compile

### Option A: SDK already installed

```bash
dotnet --info
dotnet restore src/wacs.slnx
dotnet build src/wacs.slnx -c Release
```

### Option B: auto-bootstrap local SDK

```powershell
pwsh -NoLogo -NoProfile -File build/compile-local.ps1
```

What `build/compile-local.ps1` does:
- Reuses existing `dotnet` when available.
- Otherwise downloads `https://dot.net/v1/dotnet-install.ps1` and installs SDK channel `10.0` into `.\.dotnet`.
- Builds `src/wacs.slnx`.
- Optional `-PublishMain` publishes `src/main/wacs.csproj` for a selected runtime (default `win-x64`).

Publish example:

```powershell
pwsh -NoLogo -NoProfile -File build/compile-local.ps1 -PublishMain -Runtime win-x64
```

> Visual Studio 2022 (17.x) does not target `net10.0`. Use CLI builds with .NET 10 SDK.

---

## Test workflow

Run lint + tests:

```powershell
# ScriptAnalyzer (same settings CI uses)
Invoke-ScriptAnalyzer -Path .\*.ps1,.\build\*.ps1,.\core\*.psm1,.\setup\*.ps1,.\setup\*.psm1,.\tests\*.ps1 -Recurse -Settings .\PSScriptAnalyzerSettings.psd1

# full QA test runner
powershell.exe -ExecutionPolicy Bypass -File tests\Run-Tests.ps1
```

Expected behavior of `tests/Run-Tests.ps1`:
- Performs a compile check when `dotnet` is available.
- Skips compile check when `dotnet` is missing.
- Skips Pester-style files when Pester is unavailable.
- Emits `[PASS]`, `[SKIP]`, `[FAIL]`, then `Summary: pass=<n> fail=<n> skip=<n>`.

Interpretation:
- `fail > 0` => non-zero exit code.
- `skip > 0` with `fail = 0` => successful run with environment-based skips.

---

## Runtime environment variables (source of truth: `core/Env-Loader.psm1`)

### Required keys

| Name | Required | Description |
|---|---|---|
| `ACME_DIRECTORY` | Yes | ACME directory URL used by reconcile/bootstrap paths. |
| `ACME_KID` | Yes | ACME EAB key identifier. |
| `ACME_HMAC_SECRET` | Yes | ACME EAB HMAC secret. |
| `DOMAINS` | Yes | Comma-separated domain list. |
| `ACME_SCRIPT_PATH` | Yes | Script path passed to `wacs --installation script`. |
| `ACME_SOURCE_PLUGIN` | No | Kept for compatibility; reconcile always issues with `--source manual`. |
| `ACME_ORDER_PLUGIN` | No | Order plugin (default `single`). |
| `ACME_STORE_PLUGIN` | No | Store plugin (default `certificatestore`). |
| `ACME_ACCOUNT_NAME` | No | Optional account profile passed to `--account`. |
| `ACME_INSTALLATION_PLUGINS` | No | Kept for compatibility; reconcile always issues with `--installation script`. |
| `ACME_CSR_ALGORITHM` | No | CSR algorithm preference (`ec` default, with automatic RSA fallback). |
| `ACME_SCRIPT_PARAMETERS` | No | Script parameter template. |
| `ACME_VALIDATION_MODE` | No | Global validation mode (default `none`). |
| `ACME_WACS_RETRY_ATTEMPTS` | No | WACS retry attempts (default `3`). |
| `ACME_WACS_RETRY_DELAY_SECONDS` | No | Delay between retries (default `2`). |
| `CERTIFICATE_CONFIG_DIR` | Yes | Directory for `certificate.env`, device configs, and policies. |
| `CERTIFICATE_DROP_DIR` | Yes | Watched folder for inbound certificate event JSON. |
| `CERTIFICATE_STATE_DIR` | Yes | Job state storage directory. |
| `CERTIFICATE_LOG_DIR` | Yes | Log output directory. |
| `CERTIFICATE_API_KEY` | Yes | API key used by HTTP listener auth (`X-API-Key` or Bearer). |

### Optional keys and defaults

| Name | Default | Behavior |
|---|---|---|
| `CERTIFICATE_VERIFY_MAX_ATTEMPTS` | `3` | Max verify retries. |
| `CERTIFICATE_ACTIVATE_TIMEOUT_MS` | `120000` | Activation timeout hint (ms). |
| `CERTIFICATE_DEFAULT_FANOUT` | `fail-fast` | Default policy fanout behavior. |
| `CERTIFICATE_SKIP_TLS_CHECK` | `0` | Set `1` to disable TLS certificate validation for connector API calls. |
| `CERTIFICATE_RETRY_MAX_ATTEMPTS` | `3` | Max connector operation retries. |
| `CERTIFICATE_RETRY_BACKOFF_MS` | `1000` | Initial backoff (ms). |
| `CERTIFICATE_HTTP_ENABLED` | `0` | Set `1` to enable native HttpListener input path. |
| `CERTIFICATE_HTTP_PREFIX` | `http://localhost:8443/` | HttpListener prefix actually consumed by `core/Http-Listener.psm1`. |
| `CERTIFICATE_DISABLE_ROLLBACK` | `0` | Set `1` to disable rollback execution. |
| `CERTIFICATE_HTTP_HOST` | `127.0.0.1` | Compatibility key; not currently used by listener startup path. |
| `CERTIFICATE_HTTP_PORT` | `8088` | Compatibility key; not currently used by listener startup path. |

### Env file resolution order

`Import-EnvFile` resolves `certificate.env` in this order:
1. explicit `-Path`
2. `CERTIFICATE_ENV_FILE`
3. `./certificate.env` (current working directory)
4. `$CERTIFICATE_CONFIG_DIR/certificate.env`

---

## First-time setup (bootstrap sequence)

`certificate-setup.ps1` calls `Initialize-CertificateConfig` immediately, so a valid env file must already exist before running setup.

1. Create `certificate.env` with all required keys.
2. Ensure `CERTIFICATE_CONFIG_DIR`, `CERTIFICATE_DROP_DIR`, `CERTIFICATE_STATE_DIR`, and `CERTIFICATE_LOG_DIR` directories exist.
3. Run once as administrator to register event source:

   ```powershell
   New-EventLog -LogName Application -Source Certificate
   ```

4. Run setup UI:

   ```powershell
   .\certificate-setup.ps1
   ```

5. Configure ACME values, devices, and deployment policies.
   - After saving ACME settings, setup now prompts: **Run initial ACME reconcile now? [Y/N]**.
   - Setup also includes **Register/Repair orchestrator task** for idempotent scheduled task registration.

Minimal env example:

```dotenv
ACME_DIRECTORY=https://acme-v02.api.letsencrypt.org/directory
ACME_KID=<kid>
ACME_HMAC_SECRET=<hmac>
DOMAINS=example.com,www.example.com
ACME_SCRIPT_PATH=dist\Scripts\New-CertificateDropFile.ps1
CERTIFICATE_CONFIG_DIR=C:\certificate\config
CERTIFICATE_DROP_DIR=C:\certificate\drop
CERTIFICATE_STATE_DIR=C:\certificate\state
CERTIFICATE_LOG_DIR=C:\certificate\logs
CERTIFICATE_API_KEY=<strong-random-token>
```

---

## Run as scheduled task

```cmd
schtasks /Create /SC MINUTE /MO 5 /TN "Certificate-Orchestrator" /TR "powershell.exe -ExecutionPolicy Bypass -File C:\certificate\certificate-orchestrator.ps1" /RU "SYSTEM"
```

---

## HTTP listener behavior

When `CERTIFICATE_HTTP_ENABLED=1`, orchestrator starts a background HttpListener job.

- Prefix: `CERTIFICATE_HTTP_PREFIX`
- Health endpoint: `GET /health` (no auth)
- Event endpoint: `POST /events` (auth required)
- Job endpoints: `GET /jobs/<renewal_id>` and `GET /jobs/status/<job_id>` (auth required)

Auth accepted:
- `X-API-Key: <CERTIFICATE_API_KEY>`
- `Authorization: Bearer <CERTIFICATE_API_KEY>`

---

## Drop directory file format

Each file should contain a certificate event matching `Assert-CertificateEvent` schema:

```json
{
  "event": "certificate.renewed",
  "renewal_id": "renewal-123",
  "deployment_policy_id": "prod-edge",
  "domain": "example.com",
  "cert_path": "C:\\certificate\\out\\cert.pem",
  "key_path": "C:\\certificate\\out\\key.pem",
  "fullchain_path": "C:\\certificate\\out\\fullchain.pem",
  "thumbprint": "ABC123",
  "issuer": "Let's Encrypt",
  "not_before": "2026-04-21T00:00:00Z",
  "not_after": "2026-07-20T23:59:59Z"
}
```

---

## Integration with simple-acme

Use Script Installation Plugin to emit Certificate drop files.

```text
--installation Script
--script "dist\Scripts\New-CertificateDropFile.ps1"
--scriptparameters "'<POLICY-ID>' {RenewalId} '{CertCommonName}' {CertThumbprint} {OldCertThumbprint} '{CacheFile}' '{CachePassword}' '{StorePath}' {StoreType}"
--source manual
--order single
--globalvalidation none
```

### Reconcile existing simple-acme state to `.env`

```powershell
.\certificate-simple-acme-reconcile.ps1
```

Preflight-only validation (no issuance/change):

```powershell
.\certificate-simple-acme-reconcile.ps1 -PreflightOnly
```

Outcomes:
- `create`: no matching renewal; creates one.
- `no-op`: renewal already matches `.env`.
- `update`: drift detected; runs cancel + reissue.

Reconciler also enforces in `%PROGRAMDATA%\simple-acme\settings.json`:
- `ScheduledTask.RenewalDays = 199`
- `ScheduledTask.RenewalMinimumValidDays = 16`

### Store plugin compatibility

| simple-acme StoreType | Typical StorePath value | Notes |
|---|---|---|
| `PfxFile` | Full path to `.pfx` | Required by connectors that import PFX artifacts directly. |
| `CertificateStore` | Certificate store location | Preferred for thumbprint-first Windows connector flows. |
| `PemFiles` | Folder with PEM artifacts | Used by file-based/reverse-proxy workflows. |
| `CentralSsl` | CCS directory path | Useful for centralized file drop workflows. |

---

## Connector reference

| Connector | Category | Requirements | Export style | Rollback |
|---|---|---|---|---|
| `iis` | A (Windows-native) | IIS role | legacy + connector aliases | Yes |
| `adfs` | A (Windows-native) | ADFS role | legacy + connector aliases | Yes |
| `rdp_listener` | A (Windows-native) | Remote Desktop Services | legacy + connector aliases | Yes |
| `rd_gateway` | A (Windows-native) | RD Gateway role | legacy + connector aliases | Yes |
| `rds_full` | A (Windows-native) | RDS deployment cmdlets | legacy + connector aliases | Yes |
| `ntds` | A (Windows-native) | Active Directory Domain Services | legacy + connector aliases | Yes |
| `sstp` | A (Windows-native) | RemoteAccess role (+ optional IIS binding path) | legacy + connector aliases | Yes |
| `winrm` | A (Windows-native) | WinRM service | legacy + connector aliases | Yes |
| `sql_server` | A (Windows-native) | SQL Server installed locally | legacy + connector aliases | Yes |
| `windows_admin_center` | A (Windows-native) | Windows Admin Center installed | legacy + connector aliases | Yes |
| `exchange` | C (local PSSession) | Exchange Management endpoint on localhost | legacy + connector aliases | Yes |
| `f5_bigip` | B (REST) | F5 iControl REST access | connector-only exports | Yes |
| `citrix_adc` | B (REST) | Citrix NITRO API access | connector-only exports | Yes |
| `kemp` | B (REST/XML) | Kemp endpoint access | connector-only exports | Yes |
| `java_keystore` | D (external dependency) | JDK/`keytool.exe` | not implemented in `connectors/` | Stub/disabled |
| `kemp_module` | D (external dependency) | Kemp PowerShell module | not implemented in `connectors/` | Stub/disabled |
| `vbr_cloud_gateway` | D (external dependency) | Veeam VBR module | not implemented in `connectors/` | Stub/disabled |
| `azure_application_gateway` | D (external dependency) | AzureRM module | not implemented in `connectors/` | Stub/disabled |
| `azure_ad_app_proxy` | D (external dependency) | AzureAD module | not implemented in `connectors/` | Stub/disabled |
| `sparx_procloud` | D (external dependency) | PowerShell 7 + external tooling | not implemented in `connectors/` | Stub/disabled |

---

## Backup and restore

```powershell
# Create backup (prompts for passphrase if omitted)
.\certificate-backup.ps1 -OutputPath C:\secure\certificate-2026-04-21.certbak

# Restore backup
.\certificate-restore.ps1 -BackupPath \\fileserver\backups\certificate-2026-04-21.certbak

# Validate backup readability without writing files
.\certificate-restore.ps1 -BackupPath .\certificate-2026-04-21.certbak -DryRun
```

---

## Troubleshooting quick hits

- `No certificate.env could be resolved`:
  - set `CERTIFICATE_ENV_FILE`, or
  - place `certificate.env` in current directory, or
  - place it under `$CERTIFICATE_CONFIG_DIR`.
- `Missing required environment keys`:
  - ensure all required keys listed above are present and non-empty.
- HTTP mode returns `401 unauthorized`:
  - send `X-API-Key` or Bearer token matching `CERTIFICATE_API_KEY`.
- Reconcile fails with missing `wacs`:
  - ensure `wacs` is installed and available on `PATH` before running `certificate-simple-acme-reconcile.ps1`.
- Reconcile fails with script path validation:
  - ensure `ACME_SCRIPT_PATH` is absolute and points to an existing script file.

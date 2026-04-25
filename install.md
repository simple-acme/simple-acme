# Simple-ACME + Certificate Hybrid Install Manual

This guide describes the operator install path for the repository's hybrid model:

- `simple-acme` handles certificate issuance/renewal.
- Certificate handles deployment orchestration using `.env` + TUI-driven connector/policy config.

---

## 1) Prerequisites

### Host requirements
- Windows Server 2019 or newer
- Windows PowerShell 5.1
- `simple-acme` (`wacs`) installed and available on `PATH`

### Repo/runtime location (example)
- `C:\certificate`

> Keep paths consistent with values in `certificate.env`.

---

## 2) Stage the repository

```powershell
cd C:\
# Copy or clone repository to C:\certificate
```

Confirm key scripts exist:
- `certificate-setup.ps1`
- `certificate-orchestrator.ps1`
- `certificate-simple-acme-reconcile.ps1`
- `certificate-backup.ps1`
- `certificate-restore.ps1`
- `dist\Scripts\New-CertificateDropFile.ps1`

---

## 3) Create and populate `certificate.env` (required before TUI)

`certificate-setup.ps1` initializes config immediately, so `certificate.env` must exist first.

### 3.1 Create env file from example

```powershell
cd C:\certificate
Copy-Item .\dist\certificate.env.example .\certificate.env
notepad .\certificate.env
```

### 3.2 Required keys

Set all required values:

- `ACME_DIRECTORY`
- `ACME_KID`
- `ACME_HMAC_SECRET`
- `DOMAINS`
- `ACME_SCRIPT_PATH`
- `CERTIFICATE_CONFIG_DIR`
- `CERTIFICATE_DROP_DIR`
- `CERTIFICATE_STATE_DIR`
- `CERTIFICATE_LOG_DIR`
- `CERTIFICATE_API_KEY`

`CERTIFICATE_API_KEY` must be a real secret value (not a placeholder string). Generate a high-entropy value, for example:

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

Then set it in `certificate.env`, e.g.

```dotenv
CERTIFICATE_API_KEY=<paste-generated-value-here>
```

### 3.3 Create directories referenced by env

```powershell
New-Item -ItemType Directory -Force -Path C:\certificate\config,C:\certificate\drop,C:\certificate\state,C:\certificate\log
```

### 3.4 Secret handling note

`certificate.env` contains plaintext secrets (`ACME_KID`, `ACME_HMAC_SECRET`). Restrict NTFS permissions to administrators/SYSTEM.

---

## 4) Run TUI configuration

```powershell
cd C:\certificate
.\certificate-setup.ps1
```

Use the menu to configure:
- **ACME settings** (env-backed)
- **Local Windows services / Network appliances** connector settings
- **Deployment policies** (`policies.json`)

### TUI behavior note

The ACME form updates ACME fields and preserves existing non-ACME values from current env state. Keep full env maintenance disciplined when editing manually.

---

## 5) Integrate `simple-acme` installation script bridge

Configure `simple-acme` to run:
- `dist\Scripts\New-CertificateDropFile.ps1`

This script writes certificate event JSON into `CERTIFICATE_DROP_DIR` for orchestrator ingestion.

---

## 6) Reconcile existing simple-acme state to `.env`

```powershell
cd C:\certificate
.\certificate-simple-acme-reconcile.ps1
```

Possible outcomes:
- `create`
- `no-op`
- `update`

Reconcile also enforces selected renewal schedule settings in `%PROGRAMDATA%\simple-acme\settings.json`.

---

## 7) Run orchestrator

### Manual run

```powershell
cd C:\certificate
.\certificate-orchestrator.ps1
```

### Scheduled task example

```cmd
schtasks /Create /SC MINUTE /MO 5 /TN "Certificate-Orchestrator" /TR "powershell.exe -ExecutionPolicy Bypass -File C:\certificate\certificate-orchestrator.ps1" /RU "SYSTEM"
```

---

## 8) Optional HTTP listener mode

Set in `certificate.env` if needed:

- `CERTIFICATE_HTTP_ENABLED=1`
- `CERTIFICATE_HTTP_PREFIX=http://localhost:8443/`
- `CERTIFICATE_API_KEY=<strong-token>`

Endpoints:
- `GET /health` (no auth)
- `POST /events` (API key/Bearer required)
- `GET /jobs/<renewal_id>` and `GET /jobs/status/<job_id>` (auth required)

---

## 9) Operator validation checklist

1. `certificate.env` resolves and includes all required keys.
2. `CERTIFICATE_*` directories exist and are writable by runtime identity.
3. `certificate-setup.ps1` launches and saves expected config artifacts.
4. `certificate-simple-acme-reconcile.ps1` completes with expected action.
5. Event files appear in drop folder during renewal/test cycle.
6. `certificate-orchestrator.ps1` logs and state updates are produced.


---

## 10) Backup and restore

`certificate-backup.ps1` creates an AES-256 encrypted backup of the full configuration (ACME credentials, API key, device configs, policies). A passphrase is required at backup time and must be stored securely — it cannot be recovered.

### Create a backup

```powershell
cd C:\certificate
.\certificate-backup.ps1 -OutputPath .\certificate-20260425.certbak
```

You will be prompted to enter and confirm a passphrase.

### Verify a backup (dry run, no changes made)

```powershell
.\certificate-restore.ps1 -BackupPath .\certificate-20260425.certbak -DryRun
```

### Restore on the same or a new machine

```powershell
.\certificate-restore.ps1 -BackupPath .\certificate-20260425.certbak
```

You will be prompted for the passphrase. On a new machine:
- ACME credentials and API key are restored from the backup.
- Device secret fields (passwords, tokens) are re-encrypted using the new machine's Windows DPAPI.
- Run `.\certificate-setup.ps1` after restore to verify connector connectivity.

> **Important:** If the backup predates this version, `CERTIFICATE_API_KEY` may be absent. In that case, a new key is auto-generated and printed — update any callers that use the old key.

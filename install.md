# Simple-ACME + Certificaat Hybrid Install Manual

This guide describes the operator install path for the repository's hybrid model:

- `simple-acme` handles certificate issuance/renewal.
- Certificaat handles deployment orchestration using `.env` + TUI-driven connector/policy config.

---

## 1) Prerequisites

### Host requirements
- Windows Server 2019 or newer
- Windows PowerShell 5.1
- `simple-acme` (`wacs`) installed and available on `PATH`

### Repo/runtime location (example)
- `C:\certificaat`

> Keep paths consistent with values in `certificaat.env`.

---

## 2) Stage the repository

```powershell
cd C:\
# Copy or clone repository to C:\certificaat
```

Confirm key scripts exist:
- `certificaat-setup.ps1`
- `certificaat-orchestrator.ps1`
- `certificaat-simple-acme-reconcile.ps1`
- `dist\Scripts\New-CertificaatDropFile.ps1`

---

## 3) Create and populate `certificaat.env` (required before TUI)

`certificaat-setup.ps1` initializes config immediately, so `certificaat.env` must exist first.

### 3.1 Create env file from example

```powershell
cd C:\certificaat
Copy-Item .\dist\certificaat.env.example .\certificaat.env
notepad .\certificaat.env
```

### 3.2 Required keys

Set all required values:

- `ACME_DIRECTORY`
- `ACME_KID`
- `ACME_HMAC_SECRET`
- `DOMAINS`
- `ACME_SCRIPT_PATH`
- `CERTIFICAAT_CONFIG_DIR`
- `CERTIFICAAT_DROP_DIR`
- `CERTIFICAAT_STATE_DIR`
- `CERTIFICAAT_LOG_DIR`
- `CERTIFICAAT_API_KEY`

### 3.3 Create directories referenced by env

```powershell
New-Item -ItemType Directory -Force -Path C:\certificaat\config,C:\certificaat\drop,C:\certificaat\state,C:\certificaat\log
```

### 3.4 Secret handling note

`certificaat.env` contains plaintext secrets (`ACME_KID`, `ACME_HMAC_SECRET`). Restrict NTFS permissions to administrators/SYSTEM.

---

## 4) Run TUI configuration

```powershell
cd C:\certificaat
.\certificaat-setup.ps1
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
- `dist\Scripts\New-CertificaatDropFile.ps1`

This script writes certificate event JSON into `CERTIFICAAT_DROP_DIR` for orchestrator ingestion.

---

## 6) Reconcile existing simple-acme state to `.env`

```powershell
cd C:\certificaat
.\certificaat-simple-acme-reconcile.ps1
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
cd C:\certificaat
.\certificaat-orchestrator.ps1
```

### Scheduled task example

```cmd
schtasks /Create /SC MINUTE /MO 5 /TN "Certificaat-Orchestrator" /TR "powershell.exe -ExecutionPolicy Bypass -File C:\certificaat\certificaat-orchestrator.ps1" /RU "SYSTEM"
```

---

## 8) Optional HTTP listener mode

Set in `certificaat.env` if needed:

- `CERTIFICAAT_HTTP_ENABLED=1`
- `CERTIFICAAT_HTTP_PREFIX=http://localhost:8443/`
- `CERTIFICAAT_API_KEY=<strong-token>`

Endpoints:
- `GET /health` (no auth)
- `POST /events` (API key/Bearer required)
- `GET /jobs/<renewal_id>` and `GET /jobs/status/<job_id>` (auth required)

---

## 9) Operator validation checklist

1. `certificaat.env` resolves and includes all required keys.
2. `CERTIFICAAT_*` directories exist and are writable by runtime identity.
3. `certificaat-setup.ps1` launches and saves expected config artifacts.
4. `certificaat-simple-acme-reconcile.ps1` completes with expected action.
5. Event files appear in drop folder during renewal/test cycle.
6. `certificaat-orchestrator.ps1` logs and state updates are produced.

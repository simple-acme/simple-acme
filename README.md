# Certificaat Hybrid Certificate Lifecycle Orchestrator

## Prerequisites
- Windows Server 2019 or newer
- PowerShell 5.1 minimum (PowerShell 7.x recommended)
- No external modules or third-party binaries required

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
Invoke-Pester .\tests\
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

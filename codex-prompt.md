# Codex Prompt — Windows-Native Certificaat Corrective Rewrite

You are implementing a **security/functional corrective rewrite** for this repository. Execute the following steps **in order**, and do not skip validation. The objective is to remove all non-Windows-native dependencies from the Certificaat orchestrator path while fixing all known audit issues.

## Hard constraints (apply to every change)

1. Target **Windows PowerShell 5.1 compatibility** throughout:
   - No `ForEach-Object -Parallel`
   - No null-coalescing `??`
   - No `ConvertFrom-Json -Depth` argument usage
2. Use only Windows-native/runtime-provided components:
   - `System.Net.HttpListener` for HTTP service
   - `certutil.exe` for PFX import/verify/hash operations
   - `schtasks.exe` for scheduled/service registration operations where relevant
   - `wevtutil.exe` and/or `Write-EventLog` for operational events
3. Security requirements:
   - Set `$ErrorActionPreference = 'Stop'` in executable script entrypoints and critical modules
   - No inline credentials in source
   - Read secrets via `[Environment]::GetEnvironmentVariable(...)`
4. File write safety:
   - Replace any remove-then-move patterns with atomic temp-file write + `Move-Item -Force`
   - Avoid TOCTOU-prone delete/recreate sequences
5. Keep implementation dependency-free from Node.js/TypeScript toolchains in orchestrator scope.

---

## Step 1 — Remove TypeScript/Node orchestrator layer

Delete the following if present:

- `packages/**` (entire directory tree)
- root `package.json`
- root `package-lock.json`
- root `tsconfig.base.json`
- root `vitest.config.ts`

Also remove now-stale references to these artifacts from docs and workflows (handled further below).

---

## Step 2 — Apply 14 critical bug fixes in PowerShell layer

Implement/fix the following behaviors exactly in existing PowerShell modules/scripts:

1. Ensure `setup/Device-Schemas.ps1` is properly dot-sourced where needed (`setup/Form-Runner.psm1`)
2. Fix `previous_artifact_ref` handling in fanout/orchestration flow
3. Remove/replace unsupported `ConvertFrom-Json -Depth` usage for PS 5.1 compatibility
4. Correct F5 connector field naming mismatch (`connectors/f5-bigip.psm1` and callers)
5. Replace rollback stub with functional rollback behavior (`core/Rollback-Engine.psm1`)
6. Fix fanout environment variable resolution in `core/Fanout-Runner.psm1`
7. Fix retry environment variable mapping in retry/fanout path
8. Ensure event-handler surfaces errors (do not swallow; throw appropriately)
9. Convert startup warning-only conditions into proper fail-fast when critical prerequisites are missing
10. Fix TUI choice field mismatch in `core/Tui-Engine.psm1`
11. Fix submenu navigation state persistence (`setup/Menu-Tree.ps1`, `certificaat-setup.ps1`)
12. Fix policy editor save/update behavior
13. Fix backup behavior when credentials are empty (`certificaat-backup.ps1`)
14. Fix restore secret detection and validation (`certificaat-restore.ps1`)

Files expected to be touched include:

- `setup/Form-Runner.psm1`
- `core/Fanout-Runner.psm1`
- `certificaat-restore.ps1`
- `setup/Device-Schemas.ps1`
- `core/Rollback-Engine.psm1`
- `core/Tui-Engine.psm1`
- `setup/Menu-Tree.ps1`
- `certificaat-setup.ps1`
- `certificaat-backup.ps1`
- `certificaat-orchestrator.ps1`

---

## Step 3 — Replace TS HTTP server with native listener

Create `core/Http-Listener.psm1` implementing a small HTTP service using `System.Net.HttpListener` with:

- Configurable bind address/port via environment variables
- Bearer token authentication (`Authorization: Bearer ...`) sourced from environment variable
- Health/status endpoint
- Endpoint(s) required by orchestrator control flow
- Structured JSON responses
- Defensive request parsing and explicit error handling

Integrate this module into `certificaat-orchestrator.ps1` and remove any TypeScript/Fastify assumptions.

---

## Step 4 — Connector coverage and IIS implementation

1. Add **full IIS connector** at `connectors/iis.psm1` using:
   - `WebAdministration` module
   - `certutil.exe` for certificate handling/import verification as needed
2. Add stub connectors for currently-missing types so orchestration can load them safely:
   - `connectors/nginx.psm1`
   - `connectors/apache.psm1`
   - `connectors/haproxy.psm1`
   - `connectors/traefik.psm1`
   - `connectors/envoy.psm1`
   - `connectors/caddy.psm1`
   - `connectors/aws-alb.psm1`
   - `connectors/barracuda.psm1`

Stub behavior requirements:

- Export expected connector functions
- Return explicit “not yet implemented” errors with stable error IDs
- Must not silently succeed

---

## Step 5 — Replace JS/Pester stub testing with dependency-free PS runner

Create `tests/Run-Tests.ps1` that:

- Runs all PowerShell test scripts in `tests/` without external frameworks
- Collects pass/fail counts
- Emits non-zero exit code on any failure
- Is compatible with Windows PowerShell 5.1

Update/align:

- `tests/Integration.Tests.ps1`
- `tests/Fanout-Runner.Tests.ps1`

so they run under the new harness and validate fixed behavior.

---

## Step 6 — Environment contract cleanup

Update `core/Env-Loader.psm1`:

- Required key list reflects new native architecture
- Optional key list reflects retry/fanout/http listener/connector config
- Validation messages are explicit and actionable

---

## Step 7 — Documentation update

Update:

- `README.md`
- `CONTRIBUTING.md`

to reflect:

- Windows-native-only orchestrator architecture
- No Node/TypeScript requirement for orchestrator path
- New test command: `powershell.exe -File tests\Run-Tests.ps1`
- Connector status (IIS implemented, others stubbed where applicable)

---

## Step 8 — CI cleanup

Update `.github/workflows/*.yml` to remove TypeScript/Node/Vitest jobs or steps that are no longer relevant to orchestrator validation.

Retain any unrelated workflow responsibilities that still apply.

---

## Mandatory verification (must pass)

After implementing the rewrite, run and ensure success:

1. `powershell.exe -File tests\Run-Tests.ps1`
2. `powershell.exe -File certificaat-setup.ps1` (manual flow: submenu navigation and config save behavior)
3. `powershell.exe -File certificaat-backup.ps1` followed by `powershell.exe -File certificaat-restore.ps1` (round-trip, no errors on PS 5.1)
4. Confirm no Node/TS orchestrator residue:
   - `Select-String -Path .\* -Recurse -Pattern 'npm|node_modules|typescript|vitest|fastify|tsconfig'`
   - Expectation: no relevant hits in active orchestrator/docs/workflow paths

If any verification fails, fix and rerun before completion.

---

## Delivery expectations

- Keep changes minimal but complete
- Preserve existing public script entrypoint names
- Ensure backward-safe defaults for new env vars
- Add comments only where they clarify security-sensitive behavior

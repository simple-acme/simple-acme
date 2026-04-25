# Hybrid certificate lifecycle architecture (Phase 1)

## Document status

- **Intent**: describe the hybrid model used by this repository (`simple-acme` issuance + Certificaat connector orchestration).
- **Current as of**: **2026-04-25 (UTC)**.
- **Volatile sections** (fork parity, upstream divergence) require periodic re-verification.

---

## Scope and assumptions

- Keep `simple-acme` local installation plugin behavior intact.
- Add remote/external deployment orchestration through certificate event fan-out.
- Do not replace native local installation plugins for local targets.

---

## Architecture overview

### Local execution path (unchanged)

`simple-acme -> installation plugin -> local system`

Typical local targets:
- IIS
- RDS / RD Gateway / listener
- Exchange (local management endpoint)
- Other Windows roles via local scripts/cmdlets

### Hybrid/remote execution path

`simple-acme -> script bridge -> certificate event JSON -> Certificaat orchestrator -> connector fan-out`

Typical remote targets:
- ADC/load balancers
- firewall appliances
- API-managed endpoints

The orchestrator pipeline is step-based per connector:
1. `probe`
2. `deploy`
3. `bind`
4. `activate`
5. `verify`
6. `rollback` (on failure path when applicable)

---

## Event model used by orchestrator

Certificate event shape is validated by `core/Types.ps1` (`Assert-CertificateEvent`).

Required fields:
- `event`
- `renewal_id`
- `deployment_policy_id`
- `domain`
- `cert_path`
- `key_path`
- `fullchain_path`
- `thumbprint`
- `issuer`
- `not_before`
- `not_after`

This schema is the source of truth for drop files and HTTP-submitted event payloads.

---

## Connector contract (repository reality)

Connectors are PowerShell modules under `connectors/`.

Observed export patterns:
1. **Dual exports** (common): legacy lifecycle names plus `Connector*` aliases.
2. **Connector-only exports** (some REST connectors): `Invoke-<Type>Connector*`.

Each connector participates in the same lifecycle semantics (`probe/deploy/bind/activate/verify/rollback`) and receives a hashtable context object.

---

## Capability snapshot

| Capability | Status | Notes |
|---|---|---|
| ACME issuance/renewal | Present | Provided by `simple-acme` runtime and plugins. |
| Event ingestion from script bridge | Present | Drop directory + optional HttpListener mode. |
| Policy-based connector fan-out | Present | Policy file resolves connector jobs by `deployment_policy_id`. |
| Step-level state tracking | Present | Job state persisted in `CERTIFICAAT_STATE_DIR`. |
| Unified rollback framework | Partial | Available, behavior depends on connector + `CERTIFICAAT_DISABLE_ROLLBACK`. |
| External-dependency connector families | Partial | Several listed as stub/disabled dependency classes. |

---

## Upstream/fork parity claims (time-sensitive)

Historical statement in this document previously said upstream and this fork were identical on **2026-04-21 UTC**.

Treat this as dated historical context only. Re-check before making decisions.

### Reproducible verification commands

Run from a clone of this repo:

```bash
git remote add upstream https://github.com/simple-acme/simple-acme.git  # one-time
git fetch origin
git fetch upstream

git rev-parse origin/main
git rev-parse upstream/main

git log --oneline --left-right upstream/main...origin/main
```

Interpretation:
- no commits shown in `...` range => currently identical at compared refs
- commits on either side => divergence exists and must be reviewed

Record the verification date in PR notes when updating this section.

---

## Operational risks and controls

### Risks
- Connector behavior drift vs documented contract.
- Stale env/config docs causing setup failure.
- Undetected upstream divergence changing assumptions.

### Controls
- Keep README env tables aligned with `core/Env-Loader.psm1`.
- Keep HTTP auth docs aligned with `core/Http-Listener.psm1`.
- Keep contributor connector template aligned with current connector exports.
- Re-run parity commands above when modifying architecture claims.

---

## Phase-1 recommendations

1. Preserve local plugin-first philosophy for Windows-native targets.
2. Keep remote connectors event-driven and policy-based.
3. Prioritize consistent connector export conventions for new modules.
4. Treat all dated parity claims as operational metadata, not permanent truth.

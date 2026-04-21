# Hybrid certificate lifecycle architecture (Phase 1)

## Scope and assumptions
- Scope: this document extends `simple-acme` for **remote/external** certificate targets without changing the existing local plugin-first model.
- Assumption: this repository is intended to remain aligned with upstream `simple-acme` on `main` unless intentionally diverged.
- Non-goal: replacing native installation plugins and script plugins for local systems.

## 1) Fork analysis (Bankeybiharidassa/simple-acme)

### Core architecture
- Dependency injection wires core services, ACME client services, plugin loader, renewal manager, and OS-specific scheduler in one process (`wacs`).
- Runtime plugin discovery supports source/target, validation, order, CSR, store, installation, plus secret and notification plugin classes.
- Renewals are the unit of orchestration and persist target, validation, store and installation plugin choices together.

### ACME flow
1. Load/construct ACME directory client.
2. Load existing account or create new account.
3. Optionally perform EAB flow if required by server metadata.
4. Create/reuse orders and validate authorizations.
5. Finalize certificate and store/install.
6. Persist renewal history and due-date metadata.

### Plugin and script model
- Installation flow is sequential and direct: generated cert is passed to each selected installation plugin.
- Script installation plugin invokes external `.ps1/.exe/.bat/.sh` using token replacement.
- Pre/post execution scripts are available through settings, i.e. hooks around renewal execution.

### Certificate storage and renewal data
- Renewal files track plugin options, history, account reference and protected PFX password.
- Order artifacts are cached on disk for rate-limit protection.
- Store plugins (e.g. certificate store / key vault / user store) decide where certificates persist.

### Scheduling and ARI
- Runtime due-date calculation supports server-provided renewal windows (`renewalInfo.SuggestedWindow`) and can be disabled via settings.
- Renewal timing includes randomized execution across a due-date range.

### Secret handling
- `vault://provider/key` references are resolved via secret providers.
- Secrets can be stored and retrieved through configured secret service backends.
- Renewal secrets and PFX passwords are represented as protected strings.

### Windows integration
- Windows task scheduler integration is first class.
- IIS has a native installation plugin.
- RDS, Exchange, SQL and other Windows workloads are represented by bundled PowerShell scripts in `dist/Scripts` invoked via script installation plugin.

## 2) Diff vs upstream
- `main` branch comparison (`simple-acme/simple-acme` vs `Bankeybiharidassa/simple-acme`) is currently **identical** at analysis time (2026-04-21 UTC).
- Risk: if your product depends on fork-only behavior, there is currently no branch-level divergence to protect that behavior.
- Risk: future divergence without an explicit sync strategy can break compatibility with upstream plugin contracts and renewal schema evolution.

## 3) Capability matrix

| Feature | Present | Partial | Missing | Notes |
|---|---:|---:|---:|---|
| ACME v2 | ✅ |  |  | Uses ACME protocol client and directory/account/order lifecycle. |
| ARI | ✅ |  |  | Server-side renewal window consumed when available. |
| Validation plugins | ✅ |  |  | Broad DNS/HTTP validation plugin ecosystem. |
| Installation plugins | ✅ |  |  | Native IIS + generic script plugin for custom deploys. |
| Scripting hooks | ✅ |  |  | Script installation plugin and pre/post execution settings hooks. |
| Event model |  | ✅ |  | Notification targets exist, but no certificate event bus contract for connectors. |
| Deployment abstraction |  | ✅ |  | Local plugin abstraction exists; remote connector abstraction absent. |
| Retry | ✅ |  |  | ACME retries/backoff + validation retry knobs. |
| Rollback |  | ✅ |  | Limited rollback pattern in scripts/plugins; no unified remote rollback contract. |
| State tracking |  | ✅ |  | Renewal history and due dates present; no remote device desired/actual state model. |
| Windows support | ✅ |  |  | Task scheduler, cert store integration, extensive Windows scripts. |

## 4) Hybrid model design (critical)

### Local execution path (unchanged philosophy)
`simple-acme -> installation plugin -> local system`

Use for:
- IIS
- NGINX (via local script/plugin flow)
- Apache (via local script/plugin flow)
- HAProxy (via local script/plugin flow)
- RDSGW (PowerShell)

Rules:
- Keep existing plugin model and script plugin token behavior.
- Add new local script assets if needed, but avoid introducing connector indirection for local workloads.

### Remote execution path (new)
`simple-acme -> certificate event -> connector orchestrator -> remote system`

Use for:
- Firewalls
- Load balancers / ADC appliances

Rules:
- Remote orchestration receives certificate metadata and artifacts after issuance/renewal.
- No change to existing local installation plugin semantics.

## 5) Connector model (remote only)

```ts
interface Connector {
  probe(ctx): Promise<ProbeResult>
  deploy(ctx): Promise<DeployResult>
  bind(ctx): Promise<BindResult>
  activate(ctx): Promise<ActivateResult>
  verify(ctx): Promise<VerifyResult>
  rollback(ctx): Promise<RollbackResult>
}
```

### Triggering strategy
- Add a **single optional remote-publish installation plugin** (or post-renewal notifier target) that emits a normalized certificate event.
- This plugin does not replace existing installation plugins; it can be appended after them, or used standalone for remote renewals.

### Coexistence strategy
- Local-only renewals: continue current behavior (`iis`, `script`, etc.) without remote event generation.
- Hybrid renewals: combine local installation plugin(s) + remote-publish plugin in the same renewal.

### Multiple connectors per certificate
- Event payload includes a deployment policy id.
- Orchestrator resolves that policy to N connector jobs (fan-out).
- Each connector persists step-level status and correlation IDs for retry/rollback.

## 6) Minimal event model (remote only)

Events:
- `certificate.issued`
- `certificate.renewed`
- `certificate.failed`

Payload:

```json
{
  "domain": "example.com",
  "cert_path": "...",
  "key_path": "...",
  "fullchain_path": "...",
  "thumbprint": "...",
  "issuer": "...",
  "not_before": "...",
  "not_after": "..."
}
```

Implementation notes:
- Emit events only for renewals with remote deployment enabled.
- Keep local plugin execution fully synchronous/direct as today.

## 7) RDS Gateway phase-1 implementation
- Deliverable script: `dist/Scripts/certificaat-rdsgw.ps1`.
- Design goals:
  - native simple-acme CLI flow;
  - supports EAB (KID/HMAC) + custom ACME directory endpoint;
  - exports/imports PFX to LocalMachine\My;
  - applies `Set-RDCertificate` for roles `RDGateway`, `RDWebAccess`, `RDRedirector`, `RDPublishing`;
  - restarts gateway services where required;
  - verifies role bindings;
  - idempotent (no-op when same thumbprint already active).

## 8) Multi-vendor connector roadmap

| Vendor | Auth | Upload | Format | Bind | Activate | Verify | Rollback | Tier |
|---|---|---|---|---|---|---|---|---|
| Palo Alto | API key / OAuth | XML/REST import | PEM/PFX | profile attach | commit | cert + profile readback | restore previous object | 2 |
| FortiGate | API token | REST certificate import | PEM/PFX | vpn/ssl profile ref | execute config save | endpoint config + expiry | rebind previous cert | 2 |
| Sophos | API token | REST upload | PEM/PFX | service/cert map | apply policy | API status + live TLS probe | restore previous cert id | 2 |
| Cisco (ASA/FTD) | API token/SSH | API/CLI import | PEM/PFX | trustpoint binding | write mem/deploy | show run + TLS probe | trustpoint rollback | 3 |
| Juniper | NETCONF/API | upload cert/key | PEM | service profile bind | commit confirmed | candidate vs running + probe | commit rollback | 3 |
| Check Point | API key | mgmt API import | PEM/PFX | HTTPS inspection / gateway | publish + install policy | object state + policy task | reinstall previous policy | 3 |
| Clavister | REST/API | cert object upload | PEM/PFX | policy binding | commit | object + session test | prior object restore | 3 |
| WatchGuard | API credentials | upload cert | PFX | policy binding | apply/restart policy | config API + probe | restore backup profile | 3 |
| Barracuda | REST/API token | cert upload | PEM/PFX | service assignment | apply changes | service cert readback | previous cert reassignment | 2 |
| F5 BIG-IP | token | iControl REST import | PEM/PFX | clientssl profile update | config sync | tmsh/profile verify + TLS probe | transactional rollback / old profile | 1 |
| Kemp | API token | REST upload | PFX/PEM | VS certificate set | apply | VS cert details + probe | switch to previous cert | 1 |
| Citrix ADC | NITRO token | install cert-key | PEM/PFX | bind to vserver | save ns config | binding query + probe | rebind old cert-key | 1 |
| A10 | AXAPI token | ssl cert import | PEM/PFX | template bind | write memory | template and vport readback | previous template rebind | 2 |

Tier definition:
- Tier 1: mature API, predictable transaction model.
- Tier 2: moderate API complexity and partial transactional behavior.
- Tier 3: brittle/fragmented APIs, multi-step commit semantics, higher rollback risk.

## 9) Architecture decision

Recommendation: **B) hybrid**.

Why:
- Preserves core `simple-acme` simplicity and installed-base compatibility for local targets.
- Adds only the minimal remote abstraction necessary for stateful external systems.
- Avoids cost/risk of full platform split while enabling phased connector rollout.

Not recommended for phase 1:
- A) pure direct extension only: becomes messy for remote orchestration/state/rollback.
- C) separate hub now: overkill before validating connector operational model.

## Implementation next steps (concrete)
1. Introduce remote publish extension point (plugin/notification) with opt-in config.
2. Define connector execution contract + persistence schema in external orchestrator.
3. Build Tier-1 connectors first (F5, Kemp, Citrix ADC).
4. Keep RDSGW on native script path (`certificaat-rdsgw.ps1`) without connector dependency.

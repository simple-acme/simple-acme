# Connector Orchestrator (simple-acme compatible)

This repository now contains a standalone connector orchestrator that handles post-issuance certificate deployment to remote appliances.

## Setup

1. Install dependencies:
   ```bash
   npm install
   ```
2. Create a `policies.json` file (or set `POLICY_FILE`).
3. Export required environment variables:
   - `DB_PATH` (required)
   - `POLICY_FILE` (default `./policies.json`)
   - `VERIFY_MAX_ATTEMPTS` (default `3`)
   - `ACTIVATE_TIMEOUT_MS` (default `120000`)
   - `HOST` (default `0.0.0.0`)
   - `PORT` (default `3000`)
4. Run tests:
   ```bash
   npm test
   ```

## HTTP API

- `POST /events` accepts certificate lifecycle events.
- `GET /jobs/:renewal_id` returns all jobs for a renewal.
- `GET /jobs/status/:job_id` returns one job.
- `GET /health` returns `{ "status": "ok" }`.

## Policy file format

```json
[
  {
    "policy_id": "example-policy",
    "fanout_policy": "fail-fast",
    "connectors": [
      {
        "connector_type": "f5_bigip",
        "label": "F5 prod",
        "settings": {
          "host": "f5.example.com",
          "token_env": "F5_API_TOKEN",
          "ssl_profile": "clientssl"
        }
      }
    ]
  }
]
```

`settings` keys ending in `_env` are resolved from environment variables at runtime.

## Adding a new connector

1. Create `packages/connectors/<vendor>/src/index.ts`.
2. Implement the `Connector` interface from `@orchestrator/core`.
3. Register it in your orchestrator startup wiring using `ConnectorRegistry.register(type, connector)`.
4. Add integration tests under `packages/orchestrator/tests/integration`.

## RDS Gateway script

A standalone script is available at `dist/scripts/certificaat-rdsgw.ps1` for direct invocation by the certificate manager CLI.

# Level27 DNS validation plugin (dns-01)

This plugin enables DNS-01 validation using the [Level27](https://www.level27.be/)
DNS API. Level27 is a Belgian managed hosting provider.

It creates and removes the `_acme-challenge` TXT records automatically using the
Level27 REST API:

-   `GET    domains?filter={domain}` — find the zone
-   `POST   domains/{id}/records` — create a TXT record
-   `GET    domains/{id}/records?type=TXT` — list TXT records
-   `DELETE domains/{id}/records/{recordId}` — remove a TXT record

Authentication is done by sending the API key in the `Authorization` HTTP
header.

## Requirements

-   A Level27 API key. Create one in the Level27 control panel under
    [My Profile → Security](https://app.level27.eu/account/profile/security).

## Usage

### Interactive

Run `wacs` and choose DNS validation. Select **Level27** and provide:

-   API key
-   (optional) API base URL (advanced)

### Unattended (CLI)

Example:

    wacs --target manual --host example.com --validation level27 --apikey "YOUR_API_KEY" --store pemfiles --pemfilespath /tmp/certs

Notes:

-   `--validation level27` selects this DNS provider.
-   `--apikey` is stored as a secret and can also reference the secret manager
    (e.g. `vault://...`).
-   Wildcard certificates (`*.example.com`) are supported.

## Configuration options

-   **API key** (required, sent as the `Authorization` header)
-   **API base URL** (optional, default: `https://api.level27.eu/v1`)

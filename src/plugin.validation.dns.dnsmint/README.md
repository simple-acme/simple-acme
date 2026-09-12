# DNSMint DNS validation plugin (dns-01)

This plugin enables DNS-01 validation using [DNSMint](https://dnsmint.com), a
DNS host that gives each account a dedicated domain and mints hostnames on it
over an API.

It publishes and withdraws the `_acme-challenge` TXT record through two calls:

-   `POST /api/httpreq/present` — publish the challenge value
-   `POST /api/httpreq/cleanup` — withdraw it

Both take `{"fqdn", "value"}`. Authentication is an API key in the
`Authorization` header as a bearer token.

There is no zone lookup and no record id. DNSMint derives the hostname from the
record name, and cleanup names the value to remove, so any TXT records the user
already had are left alone.

## Requirements

-   A DNSMint API key carrying the `dns01:write` scope. Create one under API
    keys in the dashboard. A key can be narrowed to a single hostname.

## Usage

### Interactive

Run `wacs`, choose DNS validation, select **DnsMint** and provide the API key.

### Unattended (CLI)

    wacs --target manual --host example.com,*.example.com --validation dnsmint --apikey "YOUR_API_KEY" --store pemfiles --pemfilespath /tmp/certs

Notes:

-   `--apikey` is stored as a secret and can reference the secret manager
    (e.g. `vault://...`).
-   Wildcard certificates are supported. A hostname and its wildcard validate
    against the same challenge name, so one key covers both.

## Configuration options

-   **API key** (required, sent as a bearer token)

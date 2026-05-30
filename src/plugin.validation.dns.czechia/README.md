# Czechia DNS validation plugin (dns-01)

This plugin enables DNS-01 validation using the Czechia DNS API.

It creates and removes TXT records using:

-   POST https://api.czechia.com/api/DNS/{zone}/TXT
-   DELETE https://api.czechia.com/api/DNS/{zone}/TXT

Authentication is done via the `AuthorizationToken` HTTP header.

## Requirements

-   A Czechia DNS API token
-   The DNS zone name used in the Czechia endpoint URL
    (e.g. `example.com`)

## Usage

### Interactive

Run `wacs` and choose DNS validation. Select **Czechia** and provide:

-   API token
-   Zone name (e.g. `example.com`)
-   (optional) TTL / base URI (advanced)

### Unattended (CLI)

Example:

    dotnet run --project src/main/wacs.csproj --   --target manual   --host example.com   --validation czechia   --apitoken "YOUR_TOKEN"   --zonename "example.com"   --store pemfiles   --pemfilespath /tmp/certs

Notes:

-   `--validation czechia` selects this DNS provider.
-   `--zonename` must match the DNS zone segment in the Czechia API URL.
-   `--store pemfiles --pemfilespath ...` is an example store target.

## API details

The plugin sends JSON bodies like:

    {
      "hostName": "_acme-challenge",
      "text": "DCV_TOKEN",
      "ttl": 3600,
      "publishZone": 1
    }

-   `hostName` is relative to the zone (`@` for apex).
-   `publishZone` must be `1` to publish the zone.

## Configuration options

-   **API base URI** (default: `https://api.czechia.com/api`)
-   **API token** (sent as `AuthorizationToken` header)
-   **Zone name** (required)
-   **TTL** (default: `3600`)

## Security notes

-   Avoid putting API tokens in shell history. Prefer environment
    variables or secret stores where possible.
-   When running unattended, ensure configuration storage matches your
    OS capabilities.

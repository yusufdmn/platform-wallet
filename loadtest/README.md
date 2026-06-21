# Load testing (k6) — mint-only

`wallet-load.js` hammers `POST /mint` through the API gateway, spreading credits
across 10 distinct accounts.

This branch (`loadtest/mint-throughput`) also disables the gateway rate limiter
and the MassTransit scheduled redelivery so the load actually stresses the
services rather than being capped at the edge or deferred. See "Branch changes"
below.

## Phases (one run)

1. **setup** (once): fetch a client_credentials token.
2. **mint** (fixed rate): `POST /mint` to one of 10 accounts with a random
   amount, each with a fresh `Idempotency-Key`. Accounts are auto-created on
   first mint.

## How many requests

The mint phase uses a **constant arrival rate**, so the count is exact and
predictable:

    total requests = RATE * DURATION_SECONDS

Defaults: `RATE=50` req/s for `DURATION_SECONDS=60` = **3,000 mints**. Override
either via env var, e.g. `RATE=20 DURATION_SECONDS=30` = 600 requests. k6 caps
concurrency at `maxVUs=100`; if the server is too slow to sustain the rate, k6
backs off rather than piling on unbounded — so it won't run away with the box.

The 10 accounts are `11111111-…-111111111111` through
`aaaaaaaa-…-aaaaaaaaaaaa` (one repeated hex digit each). Mint amounts are random
in `[MINT_MIN, MINT_MAX]`, default 1–1000.

## Prereqs

- Infra up (`/run-infra`) and services running (`/run-services`).
- k6 installed: `winget install GrafanaLabs.k6`.

## Run

```powershell
./loadtest/run.ps1
```

`run.ps1` reads the repo-root `.env` and maps the values k6 needs
(`GATEWAY_URL`, `KEYCLOAK_AUTHORITY`, secret from `KEYCLOAK_CLIENT_SECRET`).
Override accounts with `-AccountIds "guid1,guid2,..."`; otherwise 10 fixed
single-digit GUIDs (`1111…`–`aaaa…`) are used.

To tune intensity, set env vars before running, e.g.:

```powershell
$env:RATE = "20"; $env:DURATION_SECONDS = "30"   # 600 mints total
./loadtest/run.ps1
```

## Branch changes (vs base branch)

- **Gateway rate limiting disabled** — `app.UseRateLimiter()` commented out in
  `src/ApiGateway/Program.cs`.
- **Scheduled redelivery disabled** — `UseScheduledRedelivery(...)` removed from
  all 5 MassTransit services. `UseMessageRetry` intervals are unchanged.

## Notes

- **Async semantics**: mint returns `202` immediately; the ledger write completes
  asynchronously. This measures **intake throughput**, not time-to-posting.
- **Metrics → Grafana**: push k6 metrics with
  `k6 run -o experimental-prometheus-rw loadtest/wallet-load.js`.

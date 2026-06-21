# Load testing (k6) — mint-only

`wallet-load.js` hammers `POST /mint` through the API gateway, spreading credits
across 10 distinct accounts.

This branch (`loadtest/mint-throughput`) also disables the gateway rate limiter
and the MassTransit scheduled redelivery so the load actually stresses the
services rather than being capped at the edge or deferred. See "Branch changes"
below.

## Phases (one run)

1. **setup** (once): fetch a client_credentials token.
2. **mint** (ramping VUs, ~1m40s): random `POST /mint` to one of 10 accounts,
   each with a fresh `Idempotency-Key`. Accounts are auto-created on first mint.

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
GUIDs (`000…0001`–`000…0010`) are used.

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

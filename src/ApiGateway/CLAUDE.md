# API Gateway (YARP) — Service Conventions

Thin edge: JWT validation, scope policies, rate limiting, idempotency caching,
correlation ID stamping, YARP forward. No business logic. No Clean Architecture
(the gateway is flat on purpose).

## Middleware order — DO NOT REORDER

```
Authentication  →  ScopePolicy  →  RateLimit  →  Idempotency  →  CorrelationId  →  YARP
```

Reordering breaks assumptions elsewhere. For example, `Idempotency` must run
**after** `RateLimit` so that replayed keys still get rate-limited; and
`CorrelationId` must run **last before YARP** so downstream services see the
header.

## Correlation ID — the only hand-written propagation in the repo

- The middleware copies `Activity.Current.TraceId.ToString()` into
  `X-Correlation-Id` on the outbound request. Nothing else in the codebase
  writes correlation headers — OpenTelemetry auto-instrumentation handles W3C
  TraceContext everywhere else.

## Scopes

- `LedgerWrite` — `POST /v1/transactions`, `POST /v1/transactions/{id}/capture`, etc.
- `LedgerRead` — `GET /v1/accounts/**`, `GET /v1/transactions/**`.
- `LedgerAdmin` — `/admin/**` (ops console, saga inspection, DLQ replay).

## Forbidden

- Business logic. If a request needs decoding beyond header/claim inspection,
  it belongs in a downstream service.
- `Microsoft.AspNetCore.Identity` / `Microsoft.Identity.*`. JwtBearer + Keycloak
  issuer is the only auth path.
- Long-lived in-process idempotency caches. Idempotency state lives in Redis
  (shared), not per-instance memory.

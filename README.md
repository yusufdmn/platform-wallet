# Platform Wallet

**What it is.** A self-hostable, single-tenant **.NET 8 double-entry ledger**
a platform drops into its own environment to track internal money — user
balances, store credit, in-app points, or held funds during checkout. The
domain is deliberately thin so the architecture stays in focus.

**What it solves and how.** Storing a balance as one column on a user row
breaks the moment you need to answer "where did this money come from?",
retry a failed write safely, or prove the books balance. Platform Wallet
replaces that with an append-only ledger (every write is two postings summing
to zero), a saga that drives multi-step flows and compensates on failure, two
layers of idempotency so retries are safe, and HMAC-signed webhooks for
reliable downstream delivery.

**Who it's for.** Platforms — gaming, e-commerce, SaaS, loyalty — that want
an internal balance system they own end-to-end, and engineers looking for a
reference wiring of these patterns in one place.

## Table of Contents

- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [API Endpoints](#api-endpoints)
- [Ops Console](#ops-console)
- [Repository Layout](#repository-layout)
- [Conventions](#conventions)
- [Tests](#tests)
- [Sample Application](#sample-application)
- [Getting Started](#getting-started)

## Architecture

### Summary

REST at the edge, gRPC behind it, RabbitMQ between writes, one Postgres per
service, Redis for cross-instance state, Keycloak for auth, and OpenTelemetry for
everything observable. Every cross-service write is asynchronous over RabbitMQ;
the only synchronous internal call is Balance Query to Ledger over gRPC, fronted
by FusionCache. Each service follows Clean Architecture
(`Domain <- Application <- Infrastructure <- Api/Worker`), enforced at build time
by `tests/ArchitectureTests` (NetArchTest).

### High-Level Architecture

<img width="1024" height="1536" alt="architecture_diagram2" src="https://github.com/user-attachments/assets/cff3f7cd-1e1f-4930-a03f-846917c3527c" />

### Key Patterns

- **Transactional Outbox**: the `transactions` and `outbox_message` rows commit
  in one `SaveChangesAsync`; MassTransit relays the message to RabbitMQ.
- **Saga orchestration with compensation**: a single `TransactionSagaStateMachine`
  drives the lifecycle and voids held funds when a capture fails.
- **Two-layer idempotency**: the gateway dedupes on `Idempotency-Key` in Redis;
  consumers dedupe on the MassTransit inbox plus `uq_posting_tx_account_phase`.
- **CQRS**: writes go through EF Core; reads use Dapper. Balance Query is
  read-only and has no `DbContext`.
- **FusionCache (L1 + L2 + backplane)**: single-flight, fail-safe, eager refresh
  at 80% TTL, replacing cache-aside thundering herds.
- **HMAC-signed webhooks**: each delivery carries `X-Signature: sha256=...` over
  the raw body.
- **Resilience escalation**: in-line Retry, then Scheduled Retries
  (`1m, 5m, 30m, 2h, 12h, 24h`), then manual replay from the Ops Console once a
  delivery lands in `failed_webhook_deliveries` (circuit breaker planned).
- **OpenTelemetry auto-propagation**: one trace ID flows
  Gateway to Intake to RabbitMQ to Saga to Ledger to Webhook, with no manual
  correlation plumbing beyond a 5-line YARP middleware that surfaces
  `X-Correlation-Id`.

### Microservices

- **ApiGateway** (YARP, flat, no Clean Architecture): JWT validation, scope
  policies, fixed-window rate limit, Redis idempotency cache, correlation-ID
  stamping, then forward. Also hosts the Ops Console and the DLQ admin endpoints.
- **TransactionIntake** (Api): the only write front door. A MediatR handler
  writes the `transactions` row and the outbox row in one transaction; status
  consumers own each state transition.
- **SagaOrchestrator** (Worker): the Automatonymous state machine. States
  `Submitted -> Processing -> Held -> Completed | Failed | VoidStranded`,
  pessimistic-concurrency saga repository, partitioned by correlation id. The
  only service where MassTransit types are allowed in the Domain layer.
- **Ledger** (Api): the double-entry source of truth. Every write goes through
  `PostingPairBuilder` (two rows, `sum(amount_signed) == 0`, identical
  `asset_code`). Append-only; reads are Dapper; exposes a gRPC balance service.
- **BalanceQuery** (Api): read-only projection. Serves balance and history,
  pulling from Ledger over gRPC behind FusionCache.
- **WebhookDispatcher** (Worker): subscribes to terminal events and POSTs signed
  callbacks to the host URL with tiered redelivery and a persisted DLQ.
- **BuildingBlocks**: `Contracts` (message records only, sole dependency
  `MassTransit.Abstractions`), `Grpc.Protos` (`.proto` + generated stubs),
  `Observability` (`AddPlatformWalletObservability(cfg, serviceName)`).

## Tech Stack

| Concern           | Technology                                                          |
|-------------------|---------------------------------------------------------------------|
| Runtime           | .NET 8, C# 12                                                        |
| Web edge          | YARP 2.2 reverse proxy                                               |
| Web framework     | ASP.NET Core minimal-API                                             |
| Auth              | Keycloak 25 + `Microsoft.AspNetCore.Authentication.JwtBearer`        |
| Persistence       | PostgreSQL 16, database-per-service                                  |
| ORM (writes)      | EF Core 8 + Npgsql                                                   |
| ORM (reads)       | Dapper                                                               |
| Messaging         | RabbitMQ 3.13 + MassTransit 8.2                                      |
| Saga              | MassTransit Automatonymous + EF Core saga repository                |
| Cache             | Redis 7 + FusionCache (L1 + L2 + backplane)                          |
| Inter-service RPC | gRPC (Balance Query to Ledger)                                       |
| Outbound HTTP     | `IHttpClientFactory` + Polly                                         |
| Validation        | FluentValidation                                                    |
| Mediator          | MediatR (Application layer only)                                     |
| Observability     | OpenTelemetry to OTel Collector to Jaeger + Seq + Prometheus + Grafana |
| Logging           | Serilog to OTLP to OTel Collector to Seq                            |
| Containerization  | Docker Compose (single-host self-host, no Kubernetes)               |
| Config            | `.env` (via `DotNetEnv`), `appsettings.json`, env vars              |
| Testing           | xUnit, FluentAssertions, NSubstitute, Testcontainers, Respawn, NetArchTest, FsCheck |

## API Endpoints

All requests go through the gateway (`http://localhost:14041` in dev). Data-plane
calls carry an `api-version: 1` header and a Bearer JWT; writes also require an
`Idempotency-Key` header.

**Data plane**

| Method | Path                            | Scope          | Description                  |
|--------|---------------------------------|----------------|------------------------------|
| POST   | `/mint`                         | `ledger:write` | Credit an account from `@world` |
| POST   | `/burn`                         | `ledger:write` | Debit an account to `@world` |
| POST   | `/transfer`                     | `ledger:write` | Hold funds into `@held_pool` |
| POST   | `/transfer/{id}/capture`        | `ledger:write` | Settle a hold to the destination |
| POST   | `/transfer/{id}/void`           | `ledger:write` | Release a hold to the source |
| GET    | `/transactions/{id}`            | `ledger:read`  | Transaction status           |
| GET    | `/accounts/{id}/balance`        | `ledger:read`  | Current balance and held amount |
| GET    | `/accounts/{id}/history`        | `ledger:read`  | Paginated posting history    |

**Admin plane** (`ledger:admin`)

| Method | Path                                      | Description                       |
|--------|-------------------------------------------|-----------------------------------|
| GET    | `/admin/invariants/zero-sum`              | Zero-sum invariant sweep          |
| GET    | `/admin/sagas`                            | List sagas (filter by state)      |
| GET    | `/admin/sagas/{id}`                       | Saga detail                       |
| POST   | `/admin/transactions/{id}/retry-void`     | Re-drive a `VoidStranded` saga    |
| GET    | `/admin/webhooks/failed`                  | List failed webhook deliveries    |
| POST   | `/admin/webhooks/{id}/retry`              | Retry one failed delivery         |
| POST   | `/admin/webhooks/replay-all`              | Throttled replay of failures      |
| GET    | `/admin/dlq`                              | List dead-letter queues           |
| GET    | `/admin/dlq/{queue}`                      | Peek messages in a queue          |
| POST   | `/admin/dlq/{queue}/replay-one`           | Replay one dead-lettered message  |
| POST   | `/admin/dlq/{queue}/replay-all`           | Replay a queue (requires confirm) |

## Ops Console

The Ops Console (Sagas inspector, DLQ browser, Failed Webhooks list/retry/replay-all)
and the entire `/admin/**` API are **not** exposed on the public gateway port.

The gateway runs two Kestrel listeners:

| Plane | Port (dev) | Reachable from | Routes |
|---|---|---|---|
| Public data plane | `14041` | Anywhere | `/mint`, `/burn`, `/transfer/**`, `/accounts/**`, `/transactions/**` |
| Internal admin plane | `14044` | **Only the host the gateway runs on, or via an SSH/VPN tunnel into that host** | `/console/**`, `/admin/**` |

The admin listener is intended to be bound to a private interface (loopback or
a private LAN NIC) and **never published to the internet**. To use the Ops
Console from your workstation, either:

- run a browser on the gateway host itself, or
- open an SSH tunnel, e.g.
  `ssh -L 14044:localhost:14044 user@gateway-host`, then browse
  `http://localhost:14044/console/`, or
- connect through your VPN / bastion so your machine lives inside the same
  private network as the gateway.

An `AdminPlaneGuardMiddleware` enforces this at the application layer: any
`/console` or `/admin` request that arrives on the public port is rejected
with `404`, so the admin surface cannot leak even if a route or proxy is
misconfigured. In production, set `OpsConsole:InternalListenerPort` (default
`8081`) and bind that listener to a private interface only.

Login is OAuth 2.0 Authorization Code + PKCE against the `ops-console` Keycloak
client with scope `ledger:admin`.

Infra UIs (no app auth):

| Tool           | URL                          |
|----------------|------------------------------|
| Jaeger         | http://`<infra-host>`:16686  |
| Seq            | http://`<infra-host>`:5341   |
| Grafana        | http://`<infra-host>`:3000   |
| RabbitMQ Mgmt  | http://`<infra-host>`:15672  |
| Keycloak Admin | http://`<infra-host>`:8088   |

## Repository Layout

```
PlatformWallet/
├── src/
│   ├── ApiGateway/              YARP edge + Ops Console (wwwroot/console)
│   ├── BalanceQuery/            CQRS read side (Domain/Application/Infrastructure/Api)
│   ├── Ledger/                  Double-entry core (Domain/Application/Infrastructure/Api)
│   ├── SagaOrchestrator/        State machine (Domain/Application/Infrastructure/Worker)
│   ├── TransactionIntake/       Outbox write front door (Domain/Application/Infrastructure/Api)
│   ├── WebhookDispatcher/       HMAC delivery + DLQ (Domain/Application/Infrastructure/Worker)
│   └── BuildingBlocks/
│       ├── Contracts/           Message records (MassTransit.Abstractions only)
│       ├── Grpc.Protos/         .proto files + generated stubs
│       └── Observability/       AddPlatformWalletObservability()
├── tests/                       9 projects (Architecture, Unit, per-service Integration, EndToEnd)
├── deploy/
│   ├── docker-compose.infra.yml LAN infra (Postgres, RabbitMQ, Redis, Keycloak, OTel, Jaeger, Seq, Prometheus, Grafana)
│   ├── docker-compose.yml       Application services + webhook-sink
│   ├── postgres/                init-databases.sql (intake_db, saga_db, ledger_db, webhook_db, keycloak_db)
│   ├── keycloak/                realm-export.json (clients, scopes, seed users)
│   └── otel/ prometheus/ grafana/  Observability config
├── .env.example
├── Directory.Packages.props     Central Package Management
└── PlatformWallet.sln
```

## Tests

```bash
# Unit + Architecture + per-service Integration (Testcontainers)
dotnet test --filter "Category!=EndToEnd"

# End-to-end against a live compose stack
E2E_BASE_URL=http://localhost:14041 dotnet test tests/EndToEnd.Tests
```

The Ledger integration suite runs the **zero-sum invariant sweep** in fixture
teardown: if any `(tx_id, phase)` pair in `postings` sums to non-zero, the suite
fails. This is the single correctness gate for the system.

## Conventions

Enforced repo-wide by the architecture tests and review subagents:

- No EF Core, MediatR, MassTransit, `HttpClient`, or ASP.NET Core in any
  `*.Domain` project (sole carve-out: `SagaOrchestrator.Domain` for
  `MassTransit.Abstractions`).
- `BuildingBlocks/Contracts` depends on exactly one package:
  `MassTransit.Abstractions`. No EF, no entities, no logic.
- The ledger is append-only: no `UPDATE` or `DELETE` against `postings`.
- No database triggers. Invariants live in `PostingPairBuilder` and the sweep.
- No synchronous cross-service writes; every write goes through the outbox.
- Auth is Keycloak + `JwtBearer` only.
- One service owns one database.
- Migrations run via `IHostedService` in Infrastructure; Api never references EF.
- No manual correlation-ID plumbing outside the gateway middleware.

## Sample Application

A reference integration lives under `samples/UberEatsWallet/` — a small **ASP.NET
Core MVC** app (Clean Architecture: `Domain` / `Application` / `Infrastructure`
/ `Web`) that uses Platform Wallet as its money backend (mint promos, hold on
checkout, capture on delivery, void on cancel). Use it as a worked example of
how a host application talks to the ledger. See `samples/UberEatsWallet/README.md`
for run instructions.

## Getting Started

This walkthrough brings the whole stack up with Docker Compose only — no
`dotnet run` needed.

### Prerequisites

- Docker with Docker Compose v2
- `curl` and `jq` (for the smoke test below)
- A host that will run the infra. This can be `localhost` or a LAN server
  reachable from your machine.

### 1. Configure environment variables

Copy the template and fill in real values:

```bash
cp .env.example .env
```

Open `.env` and set:

- `INFRA_HOST` — the IP of the machine running infra (use `127.0.0.1` if local).
  Then replace every `<server-lan-ip>` in the file with the same value
  (including inside `REDIS_CONNECTION` and `KEYCLOAK_AUTHORITY`).
- `POSTGRES_USER` / `POSTGRES_PASSWORD` — Postgres superuser credentials.
- `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASSWORD` — RabbitMQ credentials;
  also update `RABBITMQ_MGMT_URL` to point at the same host.
- `REDIS_PASSWORD` — must match the password baked into `REDIS_CONNECTION`.
- `KEYCLOAK_ADMIN_PASSWORD`, `OPS_ADMIN_PASSWORD`, `GRAFANA_ADMIN_PASSWORD`.
- `WEBHOOK_HMAC_SECRET` — any random 32+ byte string.

There is exactly one `.env` file, at the repo root. Both compose files read it.

### 2. Bring up the infrastructure

```bash
docker compose -f deploy/docker-compose.infra.yml --env-file .env up -d
```

Starts Postgres 16, RabbitMQ 3.13, Redis 7, Keycloak 25 (with
`realm-export.json` preloaded), the OpenTelemetry Collector, Jaeger, Seq,
Prometheus, Grafana, and a local webhook-sink. Wait until Keycloak's log shows
`Running the server in development mode` before continuing.

### 3. Bring up the application services

```bash
docker compose -f deploy/docker-compose.yml --env-file .env up --build -d
```

Builds and starts the six services (Gateway, TransactionIntake,
SagaOrchestrator, Ledger, BalanceQuery, WebhookDispatcher). Migrations run
automatically via `IHostedService` inside each service on first start.

### 4. Smoke test: get a token and mint

```bash
TOKEN=$(curl -s -X POST \
  "http://<infra-host>:8088/realms/platform-wallet/protocol/openid-connect/token" \
  -d "grant_type=client_credentials" \
  -d "client_id=ledger-service-client" \
  -d "client_secret=ledger-service-secret" \
  -d "scope=ledger:read ledger:write" | jq -r .access_token)

curl -X POST http://localhost:14041/mint \
  -H "Authorization: Bearer $TOKEN" \
  -H "api-version: 1" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{ "creditAccountId": "11111111-1111-1111-1111-111111111111",
        "amount": 1000,
        "asset": "USD" }'
```

A `202 Accepted` means the write was enqueued. Follow the trace in Jaeger or
poll `GET /accounts/11111111-1111-1111-1111-111111111111/balance` to confirm.

### 5. Reach the Ops Console

Open the **internal** gateway port (default `http://<infra-host>:14044/console/`)
from inside the private network and log in as `ops-admin` with the password set
in `OPS_ADMIN_PASSWORD`. The public port `14041` will return `404` for
`/console/**` and `/admin/**` by design.

```
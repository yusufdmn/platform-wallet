# Platform Wallet

A self-hostable, open-source **.NET 8 double-entry ledger / Platform Wallet**.
Platforms (gaming, e-commerce, SaaS, loyalty) deploy it into their own
environment to manage internal balances, store credit, or points with strict
double-entry accounting, idempotency, audit trails, and signed event delivery.
The system is **single-tenant**: the host owns the deployment, the database, and
the data. The domain is intentionally thin so the architecture stays in focus.

Value verbs: **Mint**, **Burn**, **Hold**, **Capture**, **Void**, **Balance**,
**History**. Every write produces two postings whose signed amounts sum to zero,
and a runtime sweep (`GET /admin/invariants/zero-sum`) verifies that invariant
across the whole ledger.

## Table of Contents

- [Architecture](#architecture)
  - [Summary](#summary)
  - [High-Level Architecture](#high-level-architecture)
  - [Key Patterns](#key-patterns)
  - [Microservices](#microservices)
- [Tech Stack](#tech-stack)
- [API Endpoints](#api-endpoints)
- [Getting Started](#getting-started)
  - [Ops Console](#ops-console)
  - [Repository Layout](#repository-layout)
  - [Tests](#tests)
  - [Conventions](#conventions)

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

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker with Docker Compose v2
- A reachable host running the infra (a LAN box or `localhost`)
- A `.env` filled in from `.env.example`

### 1. Bring up the infrastructure

```bash
docker compose -f deploy/docker-compose.infra.yml --env-file .env up -d
```

This starts Postgres 16, RabbitMQ 3.13, Redis 7, Keycloak 25 (with
`realm-export.json` preloaded), the OpenTelemetry Collector, Jaeger, Seq,
Prometheus, and Grafana.

### 2. Run the services

Locally, with `.env` pointing at the infra host:

```bash
dotnet run --project src/Ledger/Ledger.Api
dotnet run --project src/TransactionIntake/TransactionIntake.Api
dotnet run --project src/SagaOrchestrator/SagaOrchestrator.Worker
dotnet run --project src/BalanceQuery/BalanceQuery.Api
dotnet run --project src/WebhookDispatcher/WebhookDispatcher.Worker
dotnet run --project src/ApiGateway
```

Or containerized:

```bash
docker compose -f deploy/docker-compose.yml --env-file .env up --build -d
```

### 3. Get a token and call the API

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

### Ops Console

A static SPA served by the gateway. Login is OAuth 2.0 Authorization Code + PKCE
against the `ops-console` Keycloak client (`ledger:admin`). Pages: Overview, Sagas
inspector, DLQ browser, and Failed Webhooks (list, retry, throttled replay-all).

The console and the `/admin` API are **not** served from the public edge. The gateway
runs a second, internal-only Kestrel listener for the admin plane; requests for
`/console` or `/admin` on the public port get a flat `404`. The internal listener binds
to host-loopback (`127.0.0.1:14044` in the compose deploy), so it is unreachable from
the network — operators reach it over an SSH tunnel or a host-terminating VPN:

```bash
ssh -L 14044:localhost:14044 <infra-host>
# then browse http://localhost:14044/console/
```

In local `dotnet run`, the internal listener is `http://localhost:14044` directly.

Infra UIs (no app auth):

| Tool           | URL                          |
|----------------|------------------------------|
| Jaeger         | http://`<infra-host>`:16686  |
| Seq            | http://`<infra-host>`:5341   |
| Grafana        | http://`<infra-host>`:3000   |
| RabbitMQ Mgmt  | http://`<infra-host>`:15672  |
| Keycloak Admin | http://`<infra-host>`:8088   |

### Repository Layout

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

### Tests

```bash
# Unit + Architecture + per-service Integration (Testcontainers)
dotnet test --filter "Category!=EndToEnd"

# End-to-end against a live compose stack
E2E_BASE_URL=http://localhost:14041 dotnet test tests/EndToEnd.Tests
```

The Ledger integration suite runs the **zero-sum invariant sweep** in fixture
teardown: if any `(tx_id, phase)` pair in `postings` sums to non-zero, the suite
fails. This is the single correctness gate for the system.

### Conventions

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
```
# Platform Wallet

> **Status: active development** — core transaction flows (Mint, Hold/Capture, Hold/Void) are implemented and passing end-to-end.

A **Platform Wallet / Ledger-as-a-Service** built on .NET 8.
It is a backend infrastructure component that platforms (gaming, e-commerce, SaaS,
loyalty programs) can deploy into their own environment to manage internal virtual
balances, store credit, or points with **strict double-entry accounting**, idempotency,
audit trails, and asynchronous event delivery.

The domain is intentionally thin so the architecture stays in focus: Transactional
Outbox, Saga Orchestration with compensation, CQRS, multi-layer idempotency,
cache-stampede-safe reads, HMAC-signed webhooks, and distributed tracing.

The system is **single-tenant**: the host company owns the deployment,
the database, and the data inside it.

---

## Description

The wallet exposes a small set of value-transfer verbs over HTTP:

| Verb            | What it does                                                          | Status |
|-----------------|-----------------------------------------------------------------------|--------|
| **Mint**        | Credits an account from `@world`. Money entering the system.          | ✅ Implemented |
| **Hold**        | Reserves funds on a debit account into the `@held_pool`.              | ✅ Implemented |
| **Capture**     | Settles a hold — moves the held amount to the credit account.         | ✅ Implemented |
| **Void**        | Releases a hold — returns the funds to the original debit account.    | ✅ Implemented |
| **Balance**     | Returns the current balance of an account.                            | ✅ Implemented |
| **Burn**        | Debits an account back to `@world`. Money leaving the system.         | 🔧 In progress |
| **History**     | Returns the posting history for an account / transaction.             | 🔧 In progress |

Every value-changing call goes through a **saga** that orchestrates the multi-step
flow, persists state, recovers from crashes, retries idempotently, and emits a
signed webhook to the host company once the transaction terminates.

Every write produces two postings whose signed amounts sum to **zero**. A
zero-sum invariant sweep verifies this property end-to-end after every test run
and is exposed as a runtime endpoint (`/admin/invariants/zero-sum`).

---

## Architecture

### One-line summary

> REST at the edge, gRPC behind the edge, RabbitMQ between writes, Postgres per
> service, Redis for cross-instance state, Keycloak for auth, OpenTelemetry for
> everything observable.

### Clean Architecture per service

Each service is structured as `Domain ← Application ← Infrastructure ← Api/Worker`.
The dependency arrow is one-way and is enforced at build time by `tests/ArchitectureTests`
(NetArchTest). Domain projects depend on `System.*` only — no EF Core, no
MediatR, no MassTransit, no `HttpClient`, no ASP.NET Core. The only carve-out is
`SagaOrchestrator.Domain`, which is allowed to reference `MassTransit.Abstractions`
because the Automatonymous state machine *is* the domain in that service.

### High-Level System Architechture

<img width="1024" height="1536" alt="architecture_diagram2" src="https://github.com/user-attachments/assets/b7da726f-15e6-4f6d-83fe-7d4484d90051" />


Every cross-service write is asynchronous and goes through RabbitMQ. The only
synchronous cross-service call is **Balance Query → Ledger** over gRPC, used for
the read path with **FusionCache** fronting it.

### Headline patterns

- **Transactional Outbox** — `transactions` and `outbox_message` are written in the
  same `SaveChangesAsync` in TransactionIntake; MassTransit's EF Core outbox relays
  the message to RabbitMQ. No direct `Publish`/`Send` from handlers, ever.
- **Saga Orchestration with Compensation** — `TransactionSagaStateMachine` walks
  through `Submitted → Processing → Held → Completed | Failed` in a single
  readable file. State is persisted with **pessimistic concurrency** + **partitioning
  by correlation id** (different sagas run in parallel; the same saga is serialized).
- **Two-layer idempotency** — the gateway dedupes at the network edge against
  `Idempotency-Key` cached in Redis; the consumers dedupe at-least-once redelivery
  in the inbox + DB unique constraints (`uq_posting_tx_account_phase`).
- **CQRS** — writes go through `DbContext`/EF Core; reads use raw Dapper. The Balance
  Query service is read-only and has no `DbContext`.
- **FusionCache (L1 + L2 + backplane)** — single-flight, fail-safe, soft/hard
  timeouts, eager refresh at 80% TTL. Replaces the cache-aside thundering-herd.
- **HMAC-signed webhooks with tiered redelivery** — the host company's URL is called
  with `X-Signature: sha256=…` over the raw body. `UseMessageRetry` handles transient
  faults; `UseScheduledRedelivery` provides long-tail backoff (`1m, 5m, 30m, 2h, 12h,
  24h`). After exhaustion the row lands in `failed_webhook_deliveries` for manual
  replay.
- **Tiered DLQ + fault consumers** — the saga handles `Fault<MintFunds>`,
  `Fault<HoldFunds>`, `Fault<CaptureTransfer>`, and `Fault<VoidHold>` events, driving
  the transaction to `Failed` state with a recorded reason.
- **OpenTelemetry auto-instrumentation** — one trace ID flows
  Gateway → Intake → RabbitMQ → Saga → Ledger → Webhook, no manual correlation ID
  plumbing anywhere except a single 5-line YARP middleware that copies the trace ID
  into the `X-Correlation-Id` response header.

---

## Microservices

### `ApiGateway` — YARP reverse proxy

Thin edge. JWT validation, scope policies, fixed-window rate limit (100 rpm /
remote IP), Redis-backed idempotency cache, correlation-ID stamping, then YARP
forward. No Clean Architecture and no business logic — the gateway is flat on
purpose. Middleware order (do not reorder): `Authentication → ScopePolicy →
RateLimit → Idempotency → CorrelationId → YARP`.

### `TransactionIntake` — front door for writes

The only service that accepts `POST /v1/mint`, `/v1/transfer`, `/v1/transfer/{id}/capture`,
`/v1/transfer/{id}/void`. The MediatR command handler writes the `transactions`
row **and** the `outbox_message` row in one EF Core transaction; that atomicity
is the whole point of this service. Nothing here talks to RabbitMQ directly.
Five status-update consumers (`TransactionMinted`, `Held`, `Captured`, `Failed`,
`Voided`) own each status transition with no multi-phase updates.

### `SagaOrchestrator` — Automatonymous state machine

Worker process. Coordinates the full transaction lifecycle in a single
`TransactionSagaStateMachine.cs` file. States: `Submitted → Processing → Held →
Completed | Failed`. Pessimistic-concurrency saga repository on Postgres,
partitioned receive endpoint by correlation id, defense-in-depth retry on
`DbUpdateConcurrencyException`. This is the only service where MassTransit
types are allowed in the Domain layer.

### `Ledger` — single source of truth for balances

Double-entry core. Every write goes through `PostingPairBuilder` in
`Ledger.Domain`, which enforces two rows per transaction leg with
`sum(amount_signed) == 0` and identical `asset_code`. `accounts.balance` updates
and `postings` inserts share the same `SaveChangesAsync` transaction. Reads are
Dapper-based (no `DbContext` on the read path). Exposes a gRPC service
(`LedgerGrpcBalanceService`) for the Balance Query service. Append-only:
no `UPDATE`/`DELETE` against `postings` exists anywhere in the codebase.

### `BalanceQuery` — read-only projection

CQRS read side. Queries balances on-demand via gRPC from Ledger, serves them through
FusionCache. `GET /v1/accounts/{id}/balance` is implemented; a
`GET /v1/accounts/{id}/history` (posting history) endpoint is planned. No writes,
no `DbContext`, no raw `IDistributedCache`. Soft timeout 100 ms, hard timeout 1 s,
eager refresh at 80% of TTL, fail-safe enabled (serves stale on downstream error).

### `WebhookDispatcher` — outbound HMAC delivery

Worker. Subscribes to terminal events (`TransactionMinted`, `TransactionCaptured`,
`TransactionVoided`, `TransactionFailed`) and POSTs to the host company's
configured webhook URL with an `X-Signature: sha256=…` HMAC over the raw request
body. `UseMessageRetry` handles transient faults; `UseScheduledRedelivery`
(`1m → 5m → 30m → 2h → 12h → 24h`) handles long-tail delivery failures. After
exhaustion the delivery is parked in `failed_webhook_deliveries` for manual
replay. A `transaction.burned` event type will be added alongside the Burn verb.

### `BuildingBlocks` — shared cross-cutting projects

- `Contracts` — MassTransit message records and DTOs only. **One** external
  dependency: `MassTransit.Abstractions`. Command/event pairs currently in use:
  `HoldFunds`/`FundsHeld`, `CaptureTransfer`/`TransferCaptured`,
  `VoidHold`/`HoldVoided`, `MintFunds`/`FundsMinted`. `BurnFunds`/`FundsBurned`
  are defined and awaiting consumer implementation. Terminal saga events:
  `TransactionSubmitted`, `TransactionCaptured`, `TransactionVoided`,
  `TransactionFailed`, `TransactionMinted`. `TransactionBurned` is defined but
  not yet consumed. Zero business logic, zero entity types.
- `Grpc.Protos` — `.proto` files and generated C# stubs.
- `Observability` — `AddPlatformWalletObservability(cfg, serviceName)` extension
  that wires all six OpenTelemetry instrumentations (AspNetCore, Http,
  GrpcNetClient, EntityFrameworkCore, StackExchangeRedis, MassTransit). Every
  service calls this; nothing else wires OTel by hand.

### Operator tools (`/tools`) — 🔧 Planned

- **`OpsConsole`** (`http://localhost:5555`) — static HTML + minimal-API back-end.
  PKCE login against the `ops-console` Keycloak client. Read-only views: saga
  state inspector, dead-letter queue browser (RabbitMQ Management API),
  failed-webhook list, asset registry, zero-sum invariant runner.
- **`AssetAdmin`** (`http://localhost:5556`) — separate registry tool. Maintains
  the `asset_registry` table and lists "orphan" asset codes (used on accounts but
  not registered).
- **`DevConsole`** (`http://localhost:5200`) — convenience UI used during early
  development for client-credentials token fetch + raw `POST /v1/...` proxy +
  table dump (`accounts`, `postings`, `transactions`, `outbox_message`, …).

---

## Tech stack

| Concern             | Technology                                                          |
|---------------------|---------------------------------------------------------------------|
| Runtime             | .NET 8, C# 12 (primary constructors, file-scoped namespaces)        |
| Web edge            | YARP reverse proxy                                                  |
| Web framework       | ASP.NET Core minimal-API                                            |
| Auth                | Keycloak 25 + `Microsoft.AspNetCore.Authentication.JwtBearer`       |
| Persistence         | PostgreSQL 16 per service (database-per-service)                    |
| ORM (writes)        | EF Core 8 + Npgsql                                                  |
| ORM (reads)         | Dapper                                                              |
| Messaging           | RabbitMQ 3.13 + MassTransit 8.2                                     |
| Saga                | MassTransit Automatonymous + EF Core saga repository                |
| Cache               | Redis 7 + FusionCache (L1 in-memory + L2 Redis + backplane)         |
| Inter-service RPC   | gRPC (Balance Query → Ledger)                                       |
| Outbound HTTP       | `IHttpClientFactory` + Polly                                        |
| Validation          | FluentValidation                                                    |
| Mediator            | MediatR (Application layer only)                                    |
| Observability       | OpenTelemetry → OTel Collector → Jaeger (traces) + Seq (logs) + Prometheus + Grafana |
| Logging             | Serilog → Seq (structured logs) + OTLP (traces/metrics)             |
| Containerization    | Docker Compose (no Kubernetes by design — single-host self-host)    |
| Config              | `.env` (loaded via `DotNetEnv`), `appsettings.json`, env vars       |
| Testing             | xUnit, FluentAssertions, NSubstitute, Testcontainers (Postgres + RabbitMQ + Redis), Respawn, NetArchTest, FsCheck |

---

## Functionalities

### Implemented end-to-end

These four scenarios pass on the live compose stack (`tests/EndToEnd.Tests/FullSystemFlowTests.cs`):

1. **Mint** — `POST /v1/mint` credits an account from `@world`, posts the
   double-entry pair, publishes `TransactionMinted`, and the Webhook Dispatcher
   delivers a signed `transaction.minted` callback to the host URL.
2. **Hold → Capture** — `POST /v1/transfer` reserves funds in `@held_pool`;
   `POST /v1/transfer/{id}/capture` settles the hold to the destination account
   and emits `transaction.captured`.
3. **Hold → Void** — `POST /v1/transfer/{id}/void` releases the hold back to
   the source account and emits `transaction.voided`.
4. **Zero-sum invariant** — after the three transactional flows, `GET
   /admin/invariants/zero-sum` returns `{"violations": []}`. Every
   `(transaction_id, phase)` pair in `postings` sums to zero.

### Cross-cutting features

- **Idempotency at two layers** — gateway-level cached responses against
  `Idempotency-Key` (Redis), consumer-level dedupe via the MassTransit inbox +
  `uq_posting_tx_account_phase` unique constraint. Retried `capture` / `void` calls
  return the original 2xx instead of `409 already captured`.
- **Authentication** — OAuth 2.0 Client Credentials (data plane) and Authorization
  Code + PKCE (ops console). Both flows driven by the same Keycloak realm,
  preloaded from `realm-export.json`.
- **Authorization** — scope-based, three scopes:
  - `ledger:write` — write verbs and admin invariant/saga endpoints (current gateway routing)
  - `ledger:read`  — balance and transaction queries; also used by the invariant endpoint itself
  - `ledger:admin` — defined in Keycloak and all services; gateway routing to `/admin/**`
    will be tightened to require this scope as the admin surface grows
- **Defense-in-depth scope re-validation** — every backend service re-validates
  the JWT and re-checks the scope, even though the gateway already did. The
  gateway is not a trust boundary.
- **Distributed tracing** — one trace ID per request flows through every hop
  (HTTP → MassTransit → gRPC → Postgres → Redis), visible as one waterfall in
  Jaeger.
- **Structured logging** — Serilog, enriched with service name, machine name,
  and the active trace context, sent over OTLP HTTP to Seq.
- **Metrics** — Prometheus scraper; OpenTelemetry runtime + ASP.NET + EF Core
  meters; pre-built Grafana dashboards in `deploy/grafana/dashboards`.
- **Resilience layered per transport** — Polly (retry + circuit breaker) for
  external HTTP (webhook). MassTransit `UseMessageRetry` on all consumers;
  `UseScheduledRedelivery` on the webhook endpoint. EF Core
  `EnableRetryOnFailure(5)` for Postgres. StackExchange.Redis multiplexer
  reconnect.
- **DLQ inspection from the Ops Console** — a poisoned message in
  `webhook-notifications_error` is browsable and (in Phase 2) replayable.

### Architectural guardrails enforced in CI

- `tests/ArchitectureTests` — NetArchTest rules block any reverse dependency
  arrow, any framework leak into `*.Domain`, and any business logic in
  `BuildingBlocks/Contracts`.
- `tests/Ledger.IntegrationTests` — runs the **zero-sum invariant sweep** in
  fixture teardown. This is the single correctness gate for the whole system.

---

## Repository layout

```
PlatformWallet/
├── src/
│   ├── ApiGateway/                  YARP edge — flat, no CA
│   ├── BalanceQuery/                CQRS read side (Domain/Application/Infrastructure/Api)
│   ├── Ledger/                      Double-entry core (Domain/Application/Infrastructure/Api)
│   ├── SagaOrchestrator/            Automatonymous state machine (Domain/Application/Infrastructure/Worker)
│   ├── TransactionIntake/           Outbox-backed write front door (Domain/Application/Infrastructure/Api)
│   ├── WebhookDispatcher/           HMAC outbound + tiered redelivery (Domain/Application/Infrastructure/Worker)
│   └── BuildingBlocks/
│       ├── Contracts/               Message records, DTOs, marker interfaces
│       ├── Grpc.Protos/             .proto files + generated stubs
│       └── Observability/           AddPlatformWalletObservability()
├── tests/
│   ├── ArchitectureTests/           NetArchTest layering rules
│   ├── Ledger.UnitTests/            PostingPairBuilder + invariant property tests
│   ├── Ledger.IntegrationTests/     Testcontainers + zero-sum sweep
│   ├── TransactionIntake.IntegrationTests/
│   ├── SagaOrchestrator.IntegrationTests/
│   ├── BalanceQuery.IntegrationTests/
│   ├── WebhookDispatcher.IntegrationTests/
│   ├── ApiGateway.IntegrationTests/ JWT scope-policy contract tests
│   └── EndToEnd.Tests/              Driven against the live compose stack
├── tools/                           🔧 Planned — not yet published
│   ├── DevConsole/                  Dev-time HTTP proxy + table dump (port 5200)
│   ├── OpsConsole/                  PKCE-protected ops UI (port 5555)
│   └── AssetAdmin/                  Asset registry tool (port 5556)
├── deploy/
│   ├── docker-compose.infra.yml     LAN-side infra (Postgres, RabbitMQ, Redis, Keycloak, OTel, Jaeger, Seq, Prometheus, Grafana)
│   ├── docker-compose.yml           Application services (gateway + 6 .NET services + webhook-sink)
│   ├── postgres/init-databases.sql  Creates intake_db, saga_db, ledger_db, webhook_db, keycloak_db
│   ├── keycloak/realm-export.json   Realm + clients + roles + seed users
│   ├── otel/, prometheus/, grafana/ Observability config
│   └── webhook-sink/                Tiny .NET app that records inbound deliveries (used by E2E tests)
├── .env.example                     Documents every required env var
├── Directory.Packages.props         Central Package Management
└── PlatformWallet.sln
```

---

## Getting started (🔧 demo tools are in progress...)

### Prerequisites

- .NET 8 SDK
- Docker (with Docker Compose v2)
- A reachable host running the infra (a LAN box or `localhost`)
- A copy of `.env` filled in from `.env.example`

### 1. Bring up the infrastructure

On the host that runs the infra:

```bash
docker compose -f deploy/docker-compose.infra.yml --env-file .env up -d
```

This starts Postgres 16, RabbitMQ 3.13, Redis 7, Keycloak 25 (with
`realm-export.json` preloaded), the OpenTelemetry Collector, Jaeger, Seq,
Prometheus, and Grafana.

### 2. Run the .NET services (dev workflow)

From the dev machine, with `.env` pointing at the infra host:

```bash
dotnet run --project src/Ledger/Ledger.Api
dotnet run --project src/TransactionIntake/TransactionIntake.Api
dotnet run --project src/SagaOrchestrator/SagaOrchestrator.Worker
dotnet run --project src/BalanceQuery/BalanceQuery.Api
dotnet run --project src/WebhookDispatcher/WebhookDispatcher.Worker
dotnet run --project src/ApiGateway
```

Or run everything containerized:

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

curl -X POST http://localhost:14041/v1/mint \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{ "creditAccountId": "11111111-1111-1111-1111-111111111111",
        "amount": 1000,
        "asset": "USD" }'
```

### 4. Open the operator UIs

| Tool                | URL                              | Auth            | Status         |
|---------------------|----------------------------------|-----------------|----------------|
| Ops Console         | http://localhost:5555            | PKCE login      | 🔧 Planned     |
| Asset Admin         | http://localhost:5556            | none (LAN-only) | 🔧 Planned     |
| Dev Console         | http://localhost:5200            | none (LAN-only) | 🔧 Planned     |
| Jaeger              | http://`<infra-host>`:16686      | none            | ✅             |
| Seq                 | http://`<infra-host>`:5341       | none            | ✅             |
| Grafana             | http://`<infra-host>`:3000       | admin / `.env`  | ✅             |
| RabbitMQ Mgmt       | http://`<infra-host>`:15672      | wallet / `.env` | ✅             |
| Keycloak Admin      | http://`<infra-host>`:8088       | admin / `.env`  | ✅             |

### 5. Run the tests

```bash
# Unit + Architecture + per-service Integration (Testcontainers)
dotnet test --filter "Category!=EndToEnd"

# End-to-end against a live compose stack
E2E_BASE_URL=http://localhost:14041 dotnet test tests/EndToEnd.Tests
```

The Ledger integration test suite runs the **zero-sum invariant sweep** in
fixture teardown — if any `(tx_id, phase)` pair in `postings` sums to non-zero
after a test, the suite fails.

---

## Demo flow (🔧 demo tools are in progress...)

1. **Mint** $1000 to Alice — gateway returns `202 Accepted`, the saga walks
   `Submitted → Processing → Completed`, balance is `1000`.
2. **Transfer** $200 Alice → Bob — saga walks `Submitted → Processing → Held`. Alice's
   available balance is `800`; the held amount is parked in `@held_pool`.
3. **Capture** the transfer — saga walks `Held → Completed`. Bob's balance is
   `200`, Alice's is `800`, `@held_pool` is `0`.
4. **Replay the capture** with the same `Idempotency-Key` — gateway returns the
   cached 2xx, no duplicate posting is created.
5. **Inspect the trace** in Jaeger: a single trace ID flows
   `api-gateway → transaction-intake → rabbitmq → saga-orchestrator → ledger →
   webhook-dispatcher`.
6. **Inspect the saga state** in the Ops Console — `Captured`, with timestamps
   for each transition. (The OpsConsole reads `saga_db` directly; the saga
   orchestrator is a Worker with no HTTP surface.)
7. **Run the invariant sweep** — `GET /admin/invariants/zero-sum` →
   `{"violations": []}`.

---

## Conventions enforced repo-wide

These rules are non-negotiable; the architecture tests and review subagents
block PRs that violate them:

- **No EF Core / MediatR / MassTransit / `HttpClient` / ASP.NET Core in any
  `*.Domain` project.** The only carve-out is `SagaOrchestrator.Domain` for
  `MassTransit.Abstractions`.
- **`BuildingBlocks/Contracts` depends on exactly one package**:
  `MassTransit.Abstractions`. No EF, no MediatR, no entities, no logic.
- **Database triggers are forbidden.** Invariants live in `PostingPairBuilder` +
  the zero-sum sweep, never in the database.
- **Raw `UPDATE` / `DELETE` against `postings` is forbidden.** The ledger is
  append-only.
- **No synchronous cross-service writes.** Every cross-service write goes
  through RabbitMQ via the outbox.
- **Auth is Keycloak + `JwtBearer` only.** `Microsoft.Identity.*`,
  `Microsoft.AspNetCore.Identity`, and Duende IdentityServer are forbidden.
- **One service = one database.** The schema is owned by the service that
  writes to it; nothing else opens a connection to it.
- **Migrations run via `IHostedService` in Infrastructure.** The Api project
  never references EF Core.
- **No manual correlation-ID plumbing.** The only hand-written correlation-ID
  code lives in the gateway middleware. Everywhere else, OpenTelemetry
  auto-instrumentation handles W3C TraceContext propagation.

---

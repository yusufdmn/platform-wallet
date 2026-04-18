# Platform Wallet — Repo Conventions

These rules are non-negotiable across the repo. Per-service `CLAUDE.md` files add
service-specific guardrails on top of these. See `TheMainPlan.md` for the
architectural specification; this file is the enforcement summary.

## Architecture

- **Clean Architecture per service**: `Domain ← Application ← Infrastructure ← Api/Worker`.
  The dependency arrow is one-way; `tests/ArchitectureTests` fails CI on any violation.
- **Domain** has ZERO external dependencies beyond `System.*`. No EF Core, no MediatR,
  no `MassTransit.IConsumer`, no `HttpClient`, no ASP.NET Core.
- **Exception**: `SagaOrchestrator.Domain` may reference `MassTransit.Abstractions`
  because the Automatonymous state machine **is** the domain there. This is the
  only service where MassTransit types are Domain-allowed, and the architecture
  test has an explicit carve-out for it.
- `BuildingBlocks/Contracts` depends on **exactly one** package: `MassTransit.Abstractions`.
  Only pure records / marker interfaces. No business logic.
- `BuildingBlocks` carries no business logic of any kind.

## SOLID / DRY

- One consumer = one responsibility. No multi-phase consumers. The saga orchestrates;
  consumers execute.
- Prefer primary constructors (C# 12). Entities are constructed via factory methods;
  no public setters.
- Cross-cutting helpers live in `Domain` (pure) or `Infrastructure` (adapters) —
  never duplicated per service.
- No speculative abstractions. Three similar lines beat a premature interface.

## Naming

- Projects: `<Service>.<Layer>.csproj` (e.g. `Ledger.Domain.csproj`).
- Folders mirror namespaces. PascalCase. No `Helpers/` or `Utils/` dumping grounds.
- Consumers: `<Verb>Consumer.cs`. State machines: `<Name>StateMachine.cs`.
- Messages: past-tense facts (`HoldRequested`, `CapturePosted`). No `*DTO` suffixes.

## Async

- All I/O is async. No `.Result`, no `.Wait()`.
- `CancellationToken` on every public async method.
- `ConfigureAwait(false)` only inside `BuildingBlocks` libraries; app code uses defaults.

## Testing

- xUnit + FluentAssertions + Testcontainers.PostgreSql + Testcontainers.RabbitMq.
- `Ledger.IntegrationTests` runs the zero-sum invariant sweep in fixture teardown —
  this is the single correctness gate for the whole system.
- `NetArchTest` in `tests/ArchitectureTests` encodes the CA layering rules.
- End-to-end scenarios live in `tests/EndToEnd.Tests` and spin the full
  `deploy/docker-compose.yml`.

## Forbidden

- `DbContext` / EF Core / MediatR in any `*.Domain` project.
- Business logic in `BuildingBlocks/Contracts`.
- Database triggers. Invariants live in `PostingPairBuilder` + the zero-sum sweep.
- `Microsoft.Identity.*`, `Microsoft.AspNetCore.Identity`, Duende IdentityServer.
  Keycloak + `Microsoft.AspNetCore.Authentication.JwtBearer` are the only auth path.
- Raw SQL `UPDATE`/`DELETE` against `postings`. The ledger is append-only.
- Manual correlation-ID plumbing inside services. The only hand-written
  correlation-ID code lives in `ApiGateway` middleware; everywhere else trusts
  OpenTelemetry auto-instrumentation.
- Kubernetes / Helm. Docker Compose only.
- Secrets in `appsettings.json` or `dotnet user-secrets`. Use a local `.env` file
  (git-ignored) loaded via `DotNetEnv` for dev; `.env.example` documents the keys.
- Synchronous cross-service calls for writes. Cross-service writes go through
  RabbitMQ (outbox → MassTransit → inbox).
- `AsyncLocal` / `ThreadStatic` for passing context across handlers. Use DI.

## Observability

- Every service calls `services.AddPlatformWalletObservability(cfg, serviceName)`
  from `BuildingBlocks/Observability`. All six instrumentations (AspNetCore, Http,
  GrpcNetClient, EntityFrameworkCore, StackExchangeRedis, MassTransit) are enabled
  there; services do not add their own.
- Logs go through Serilog → Seq. Traces + metrics go through OTel Collector.

## Commits

- Conventional Commits. Cite the ADR number when the change implements one:
  `feat(ledger): add PostingPairBuilder (ADR-0007)`.
- One logical change per commit. Do not batch unrelated fixes.

## Human-Required Actions

The agent cannot create secrets, approve MCP trust prompts, install Docker, or
push to a remote on your behalf. See `validated-sniffing-teacup.md` §9 for the
12-row table. Whenever a task blocks on one of those rows, the agent stops and
prints `ACTION REQUIRED — human step: ...` referencing the row number.

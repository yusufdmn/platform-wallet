# ADR-0001 — Clean Architecture per Microservice

- **Status:** Accepted
- **Date:** 2026-04-18
- **Deciders:** Platform Wallet core maintainers
- **Context:** Repo scaffolding phase. No business code yet.

## Context

Platform Wallet is a multi-service .NET 8 system (Intake, SagaOrchestrator,
Ledger, BalanceQuery, WebhookDispatcher, ApiGateway). The correctness gate for
the whole system is the zero-sum invariant on the ledger, enforced in
`PostingPairBuilder`. For the invariant to remain inspectable — and for
consumers, HTTP endpoints, and gRPC services to share the same domain primitives
without duplication — each service is organized as four projects:

```
Domain  ←  Application  ←  Infrastructure  ←  Api / Worker
```

## Decision

- Every service follows the four-layer split above.
- `Domain` has no external package references. Only `System.*`.
- `Application` references `Domain` and `BuildingBlocks/Contracts`. Consumer
  interfaces (`IConsumer<T>`, `MediatR.IRequestHandler`) live here.
- `Infrastructure` references `Application` and holds EF Core, Dapper,
  FusionCache, HTTP clients, transport config.
- `Api` / `Worker` is the composition root. `Program.cs`, DI, middleware,
  OpenTelemetry wiring.
- `NetArchTest` in `tests/ArchitectureTests` fails CI on any violation.
- **Exception:** `SagaOrchestrator.Domain` may reference
  `MassTransit.Abstractions` because the Automatonymous state machine is the
  domain there. No other Domain project gets this carve-out.
- `BuildingBlocks/Contracts` has exactly one `PackageReference`:
  `MassTransit.Abstractions`. It holds pure records and marker interfaces.

## Consequences

- Adding a new service follows a fixed template: four csproj files, four
  `ProjectReference` edges, one entry in `PlatformWallet.sln`.
- Infrastructure swaps (e.g., Dapper → raw ADO.NET) are local to
  `Infrastructure` and never ripple into Domain.
- Framework churn (ASP.NET Core major upgrades, MassTransit major upgrades)
  affects Api/Worker + Infrastructure and leaves Domain untouched.
- Integration tests against real Postgres/RabbitMQ (Testcontainers) cover the
  Infrastructure layer; Domain is unit-tested in isolation.

## Alternatives considered

- **Single project per service.** Rejected — business logic would mix with
  framework code and the invariant surface would sprawl.
- **Vertical slice per feature.** Considered for Intake (MediatR friendly), but
  rejected to keep one convention across all six services.

## Links

- `CLAUDE.md` (root) — enforcement summary.
- `tests/ArchitectureTests/CleanArchitectureRulesTests.cs` — CI gate.
- `TheMainPlan.md` §3 and §8.

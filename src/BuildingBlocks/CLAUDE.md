# BuildingBlocks — Shared Contracts & Cross-Cutting

Three projects, three roles. None of them contains business logic.

## Contracts

- **Exactly one** `PackageReference`: `MassTransit.Abstractions`.
  The `contracts-purity-reviewer` subagent fails any PR that adds a second.
- Only pure records + marker interfaces. No behavior, no validation, no
  constructors-with-logic. Messages are immutable DTOs across the wire.

## Grpc.Protos

- Codegen target. Owns `.proto` files, `Grpc.Tools`, `Google.Protobuf`.
- Do not add implementation here. Client factories live in each consumer's
  Infrastructure layer; service implementations live in the producing service's
  Application layer.

## Observability

- Exposes **one** extension method:
  `services.AddPlatformWalletObservability(configuration, serviceName)`.
  It wires the OTel bundle (AspNetCore, Http, GrpcNetClient, EntityFrameworkCore,
  StackExchangeRedis, MassTransit source) and the OTLP exporter.
- Services do not add their own OTel instrumentation. If you find yourself
  calling `.AddSource(...)` in a service `Program.cs`, the right answer is to
  extend `AddPlatformWalletObservability` and let every service benefit.

## Forbidden across all three

- Business logic of any kind.
- References to any service's Domain / Application / Infrastructure project.
  BuildingBlocks is upstream of everything; if it needs to reference a service,
  the abstraction is wrong.

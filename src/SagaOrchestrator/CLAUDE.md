# Saga Orchestrator — Service Conventions

The reason this service exists is compensation. The state machine **is** the
domain — that is why `SagaOrchestrator.Domain` is the only Domain project in
the repo allowed to reference `MassTransit.Abstractions`.

## State machine invariants

- Every transition **into** `Held` must have a corresponding compensating
  transition (typically `VoidHold` on `CaptureFailed` or timeout). No held
  funds can be orphaned.
- `ConcurrencyMode.Pessimistic` is non-negotiable. Do not switch to optimistic
  under any circumstance — concurrent captures on the same correlation id must
  serialize, not race.
- `UsePartitioner(8, x => x.Message.CorrelationId)` ensures all messages for a
  given saga instance land on the same partition. Keep the partition count at
  a power of two; resizing requires a drain.
- All five `IConsumer<Fault<T>>` consumers must be registered. Faults without
  handlers silently move to DLQ and you lose the compensation trigger.

## Retries & scheduled redelivery

- `UseMessageRetry(r => r.Intervals(100ms, 500ms, 2s))` for transient faults
  at the consumer level.
- `UseScheduledRedelivery(1m, 5m, 30m, 2h, 12h)` at the endpoint level for
  longer outages.
- Do not combine the two on the same endpoint without understanding that
  scheduled redelivery applies **after** message retry is exhausted.

## Forbidden

- Business logic in the state machine beyond transitions + activity dispatch.
  The state machine orchestrates; activities in `SagaOrchestrator.Application`
  execute.

# Transaction Intake — Service Conventions

Front door for writes. Nothing here talks directly to the Ledger — every
write goes through RabbitMQ (outbox pattern) to the saga.

## Command handler contract

- The MediatR command handler writes the `transactions` row **and** the
  `outbox_message` row in one `DbContext` `SaveChangesAsync`. That atomicity
  is the entire point of this service.
- Never publish to RabbitMQ directly. The MassTransit EF Core outbox relays
  the message. If you `Publish`/`Send` directly, the atomicity disappears.

## Status writers

- Only five consumers are allowed to mutate `transactions.status`:
  `Held`, `CaptureRequested`, `Captured`, `Failed`, `Voided`. Each consumer
  owns its phase; no multi-phase updates. Keeping this list short makes
  audit trivially inspectable.

## Idempotency

- Clients send `Idempotency-Key`. The gateway hashes + forwards it; the intake
  handler inserts into `idempotency_keys` in the same transaction as the
  `transactions` row. Duplicate keys return the cached response without
  re-executing business logic.

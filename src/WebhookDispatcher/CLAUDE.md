# Webhook Dispatcher — Service Conventions

Durable outbound webhook delivery with HMAC signing, Polly-driven retry,
MassTransit scheduled redelivery for longer outages, and a persisted DLQ.

## HMAC signing — exact contract

- Sign the **raw body bytes**, not the re-serialized JSON string. Re-serializing
  loses byte-exact reproducibility and receivers will fail signature checks.
- Header format: `X-Signature: sha256=<hex>` (lowercase hex, no prefix besides
  `sha256=`).
- The signing secret comes from `.env` as `WEBHOOK_HMAC_SECRET`. Never log it.
- `HmacSigner` in `WebhookDispatcher.Domain` is a pure function over
  `(ReadOnlySpan<byte> body, ReadOnlySpan<byte> key) → string`. No I/O.

## Retry stack

- Polly v8 (`Microsoft.Extensions.Http.Resilience`) handles the per-HTTP-call
  retry / circuit breaker for short transient failures.
- MassTransit `UseScheduledRedelivery(1m, 5m, 30m, 2h, 12h, 24h)` handles
  longer outages at the transport level.
- After scheduled redelivery is exhausted, the message hits the DLQ consumer
  which inserts a row into `failed_webhook_deliveries` with the last response
  body + status.

## Forbidden

- Logging secrets, bodies, or headers at `Information` level. Raw webhook
  bodies stay at `Debug` or `Trace`.
- Re-computing the body inside `HttpClient` handlers — sign once at enqueue
  time, persist the signature, replay verbatim on every redelivery.

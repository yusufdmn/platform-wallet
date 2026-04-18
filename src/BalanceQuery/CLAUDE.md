# Balance Query — Service Conventions

Read-only projection. No writes, no `DbContext`. Listens to ledger events,
serves cached balances through gRPC-backed FusionCache.

## FusionCache settings (bind from config, not hard-coded)

- L1 (in-memory) + L2 (Redis) + backplane. All three on.
- Soft timeout: 100 ms. Hard timeout: 1 s.
- Eager refresh at 80% of TTL. Fail-safe enabled (serve stale on downstream error).
- Single-flight is implicit in FusionCache — do not re-implement it.

## Forbidden

- Raw `IDistributedCache`. FusionCache is the only cache interface.
- Talking to the `ledger_db` directly. Reads go through the gRPC balance
  service on the Ledger API; FusionCache sits in front.
- Cache-aside patterns written by hand. `cache.GetOrSetAsync` is the only
  allowed entry point.

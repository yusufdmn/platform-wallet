# Ledger — Service Conventions

The Ledger is the single source of truth for balances. The invariants here are
the reason the whole system exists.

## Write path — non-negotiable

- Every write to `postings` goes through `PostingPairBuilder` in `Ledger.Domain`.
  The builder enforces: two rows per transaction leg, `sum(amount_signed) == 0`,
  and identical `asset_code`. There are no other code paths into `postings`.
- `accounts.balance` updates and `postings` inserts share the same
  `SaveChangesAsync` transaction. Never split them.
- `row_version` is the EF concurrency token on `accounts`. Keep it; do not
  replace with `xmin` or a custom timestamp.
- `force_fail_capture = true` in transaction metadata is a production-code
  fault switch, not an `#if DEBUG` block. Integration tests depend on it.

## Read path

- Dapper, read-only. No `DbContext` on the read side. Queries live in
  `Ledger.Infrastructure/Dapper/` and are called from `Ledger.Application`
  query handlers.

## Forbidden

- `UPDATE postings ...` / `DELETE FROM postings ...` anywhere in the codebase
  or migrations. The `sql-migration-reviewer` subagent blocks these.
- Database triggers. The zero-sum invariant is a code + test invariant, not a DB one.
- Consumers that mutate balances without going through `PostingPairBuilder`.

## Idempotency

- `idempotency_keys` has a unique index `(tenant_id, key_hash)`. The intake
  service owns writes to that table; Ledger consumers check it on inbound
  messages to short-circuit duplicates.

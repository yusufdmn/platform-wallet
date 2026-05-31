# UberEats Wallet — Platform Wallet integration demo

> **This is a sample, not a product.** Its only purpose is to demonstrate that the
> [Platform Wallet](../../TheMainPlan.md) double-entry ledger is **integrable and works end-to-end**.
> The food-ordering domain is intentionally thin and deliberately omits real UberEats economics
> (platform commission, driver payouts, multi-party splits) — those can't be done in a single ledger
> operation because `transfer` is two-party and `capture` is full-amount. What it *does* show is the
> ledger exercised through a realistic host-company app.

## What it demonstrates

A two-sided wallet — **customers** and **restaurants** — mapped onto the ledger's primitives:

| You do… | …the wallet does |
|---|---|
| Top up with a (mock) card | `mint` (auto-creates the account) |
| Place an order | `transfer` → funds **Held** |
| Restaurant accepts | `capture` (customer → restaurant) |
| Restaurant rejects / you cancel / 5-min timeout | `void` (auto-void on timeout) |
| Refund an accepted order | reverse `transfer` + `capture` |
| Withdraw to bank | `burn` |

Order status updates arrive over the wallet's **HMAC-signed webhooks** (no polling) and flip the UI live.

## Architecture

Clean Architecture, dependency arrow inward only: `Web → Infrastructure → Application → Domain`.
See [CLAUDE.md](./CLAUDE.md) for the layering rules, the integration contract, and coding standards.

- **.NET 8 / ASP.NET Core MVC**, modern UI (Tailwind CSS + htmx + Alpine.js).
- **EF Core + SQLite** for the app's own data (customers, restaurants, menus, orders). The **ledger is the
  source of truth for money** — balances are always read back, never stored locally.

## Running it

1. Bring up the wallet: infrastructure (`/run-infra`) and the six services (`/run-services`). The gateway
   listens on `http://localhost:14041`.
2. Point the wallet's webhook dispatcher at this app: in the **repo-root** `.env`, set
   `WEBHOOK_TARGET_URL=http://localhost:5080/webhooks/wallet` and restart `webhook-dispatcher`.
3. Configure this app: `cp samples/UberEatsWallet/.env.example samples/UberEatsWallet/.env` and fill it in
   (the `WEBHOOK_HMAC_SECRET` **must match** the wallet's).
4. Run it: `dotnet run --project samples/UberEatsWallet/UberEatsWallet.Web`, then open
   `http://localhost:5080`. The seeder creates demo customers, restaurants, and menus and mints opening balances.

## Configuration

All settings come from `samples/UberEatsWallet/.env` (this sample's **own** env file — not the repo-root one).
See [.env.example](./.env.example).

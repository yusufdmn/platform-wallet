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

1. Make sure the wallet is running and reachable — e.g. the containerized stack on a LAN server, with the
   gateway on `http://<server>:14041` and Keycloak on `http://<server>:8088`.
2. Configure this app: `cp samples/UberEatsWallet/.env.example samples/UberEatsWallet/.env` and fill it in —
   point `WALLET_GATEWAY_URL` / `WALLET_TOKEN_URL` at the wallet, and set `WEBHOOK_HMAC_SECRET` to **match**
   the wallet's.
3. Run it: `dotnet run --project samples/UberEatsWallet/UberEatsWallet.Web`, then open `http://localhost:5080`.
   The seeder creates demo customers, restaurants, and menus and mints opening balances.
4. (Optional — for live status) Deliver webhooks to this app: point the wallet's `webhook-dispatcher` at this
   app's reachable URL — e.g. `http://<this-machine-LAN-IP>:5080/webhooks/wallet` — and restart it, allowing
   inbound port 5080. Without this, accept/reject/refund still update the UI; only the 5-minute auto-expiry and
   hold-failure reconciliation (which rely on the webhook) won't fire.

## Configuration

All settings come from `samples/UberEatsWallet/.env` (this sample's **own** env file — not the repo-root one).
See [.env.example](./.env.example).

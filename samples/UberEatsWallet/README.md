# UberEats Wallet: Platform Wallet integration demo

> **This is a sample, not a product.** Its only purpose is to demonstrate that the
> [Platform Wallet](../../TheMainPlan.md) double-entry ledger is **integrable and works end-to-end**.
> The food-ordering domain is intentionally thin and deliberately omits real UberEats economics
> (platform commission, driver payouts, multi-party splits). What it *does* show is the
> ledger exercised through a realistic host-company app.

## What it demonstrates

A two-sided wallet (**customers** and **restaurants**) mapped onto the ledger's primitives:

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

- **.NET 8 / ASP.NET Core MVC**, modern UI (Tailwind CSS + htmx + Alpine.js).
- **EF Core + SQLite** for the app's own data (customers, restaurants, menus, orders). The **ledger is the
  source of truth for money**; balances are always read back, never stored locally.

## Running it

**1. Make sure the wallet is running and reachable.** For example the containerized stack on a LAN server,
with the gateway on `http://<server>:14041` and Keycloak on `http://<server>:8088`.

**2. Configure this app.** Copy the template and fill it in:

```bash
cp samples/UberEatsWallet/.env.example samples/UberEatsWallet/.env
```

Point `WALLET_GATEWAY_URL` and `WALLET_TOKEN_URL` at the wallet, and set `WEBHOOK_HMAC_SECRET` to **match**
the wallet's secret.

**3. Run it.**

```bash
dotnet run --project samples/UberEatsWallet/UberEatsWallet.Web
```

Then open `http://localhost:5080`. The seeder creates demo customers, restaurants, and menus, and mints
opening balances.

**4. (Optional) Deliver webhooks for live status.** Point the wallet's `webhook-dispatcher` at this app's
reachable URL and restart it, allowing inbound port 5080:

```
http://<this-machine-LAN-IP>:5080/webhooks/wallet
```

Without this, accept/reject/refund still update the UI; only the 5-minute auto-expiry and hold-failure
reconciliation (which rely on the webhook) won't fire.

## Configuration

All settings come from `samples/UberEatsWallet/.env` (this sample's **own** env file, not the repo-root one).
See [.env.example](./.env.example).

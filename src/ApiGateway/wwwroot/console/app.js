// Platform Wallet — Ops Console (PKCE, vanilla JS)
// TODO: add oidc-client-ts via CDN, perform Auth Code + PKCE against Keycloak
//       (client_id=ops-console, redirect_uri=http://127.0.0.1:5555/callback),
//       store the access token in memory, fetch /admin/sagas, /admin/failed-messages,
//       /admin/webhooks/failed, and expose a "Retry" button that calls
//       POST /admin/webhooks/{id}/retry.
//
// Per TheMainPlan.md §0.11 / §3.1: zero-weight static site, no build step.
console.info("[ops-console] scaffold — PKCE flow to be added");

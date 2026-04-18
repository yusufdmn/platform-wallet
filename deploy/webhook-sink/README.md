# Webhook Sink

Placeholder for the local webhook receiver used during §7 verification of
`TheMainPlan.md`. Will hold a tiny HTTP server (Node / .NET minimal API) that
logs raw body bytes + `X-Signature` headers so `openssl dgst` can be run
against them manually.

Not yet implemented — deferred until the webhook dispatch pipeline exists.

// Platform Wallet — Ops Console PKCE client.
// Reads runtime config from /console/config.json (no LAN IP baked into JS).
// Stores access token at localStorage["pw.access_token"] for back-compat with DLQ pages.

const TOKEN_KEY    = "pw.access_token";
const TOKEN_EXP    = "pw.access_token_exp";
const VERIFIER_KEY = "pw.pkce_verifier";
const STATE_KEY    = "pw.pkce_state";
const RETURN_KEY   = "pw.return_to";

let _config = null;

async function loadConfig() {
    if (_config) return _config;
    const res = await fetch("/console/config.json", { cache: "no-store" });
    if (!res.ok) throw new Error("config.json unavailable (HTTP " + res.status + ")");
    _config = await res.json();
    return _config;
}

function b64url(buf) {
    let s = btoa(String.fromCharCode(...new Uint8Array(buf)));
    return s.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function randomVerifier() {
    const bytes = new Uint8Array(48);
    crypto.getRandomValues(bytes);
    return b64url(bytes.buffer);
}

async function challengeFor(verifier) {
    const hash = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier));
    return b64url(hash);
}

function tokenValid() {
    const token = localStorage.getItem(TOKEN_KEY);
    const exp   = parseInt(localStorage.getItem(TOKEN_EXP) || "0", 10);
    if (!token) return false;
    return exp > Math.floor(Date.now() / 1000) + 5;
}

function authHeader() {
    const token = localStorage.getItem(TOKEN_KEY) || "";
    return { Authorization: "Bearer " + token };
}

async function redirectToLogin({ silent = false } = {}) {
    const cfg       = await loadConfig();
    const verifier  = randomVerifier();
    const challenge = await challengeFor(verifier);
    const state     = randomVerifier();

    sessionStorage.setItem(VERIFIER_KEY, verifier);
    sessionStorage.setItem(STATE_KEY, state);
    sessionStorage.setItem(RETURN_KEY, window.location.pathname + window.location.search);

    const redirectUri = cfg.redirectUri || (window.location.origin + "/console/callback.html");
    const url = new URL(cfg.keycloakAuthority.replace(/\/$/, "") + "/protocol/openid-connect/auth");
    url.searchParams.set("client_id", cfg.clientId);
    url.searchParams.set("redirect_uri", redirectUri);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("scope", cfg.scope);
    url.searchParams.set("state", state);
    url.searchParams.set("code_challenge", challenge);
    url.searchParams.set("code_challenge_method", "S256");
    if (silent) url.searchParams.set("prompt", "none");

    window.location.assign(url.toString());
}

async function requireLogin() {
    if (tokenValid()) return;
    await redirectToLogin();
}

async function handleCallback() {
    const cfg      = await loadConfig();
    const params   = new URLSearchParams(window.location.search);
    const code     = params.get("code");
    const state    = params.get("state");
    const expected = sessionStorage.getItem(STATE_KEY);
    const verifier = sessionStorage.getItem(VERIFIER_KEY);
    const returnTo = sessionStorage.getItem(RETURN_KEY) || "/console/";

    if (!code || !state || state !== expected || !verifier) {
        throw new Error("Invalid auth callback (state / verifier mismatch).");
    }

    const redirectUri = cfg.redirectUri || (window.location.origin + "/console/callback.html");
    const body = new URLSearchParams();
    body.set("grant_type", "authorization_code");
    body.set("client_id", cfg.clientId);
    body.set("code", code);
    body.set("redirect_uri", redirectUri);
    body.set("code_verifier", verifier);

    const tokenUrl = cfg.keycloakAuthority.replace(/\/$/, "") + "/protocol/openid-connect/token";
    const res = await fetch(tokenUrl, {
        method:  "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body,
    });
    if (!res.ok) {
        const detail = await res.text().catch(() => "");
        throw new Error("Token exchange failed (HTTP " + res.status + "): " + detail);
    }
    const tok = await res.json();
    const expiresIn = parseInt(tok.expires_in, 10) || 60;

    localStorage.setItem(TOKEN_KEY, tok.access_token);
    localStorage.setItem(TOKEN_EXP, String(Math.floor(Date.now() / 1000) + expiresIn));

    sessionStorage.removeItem(VERIFIER_KEY);
    sessionStorage.removeItem(STATE_KEY);
    sessionStorage.removeItem(RETURN_KEY);

    return returnTo;
}

function logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(TOKEN_EXP);
    sessionStorage.removeItem(VERIFIER_KEY);
    sessionStorage.removeItem(STATE_KEY);
    sessionStorage.removeItem(RETURN_KEY);
}

window.PwAuth = { requireLogin, authHeader, redirectToLogin, handleCallback, logout, loadConfig };

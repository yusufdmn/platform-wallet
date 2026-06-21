import http from 'k6/http';
import { check, fail } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

// ---------------------------------------------------------------------------
// Mint-only load test: hammers POST /mint, spreading credits across 10 accounts.
//
// Config (all from env — nothing secret is committed):
//   GATEWAY_URL          e.g. http://192.168.1.166:14041
//   KEYCLOAK_AUTHORITY   e.g. http://192.168.1.166:8088/realms/platform-wallet
//   LEDGER_CLIENT_ID     default: ledger-service-client
//   LEDGER_CLIENT_SECRET required
//   ACCOUNT_IDS          (optional) comma-separated GUIDs. Defaults to 10 below.
//                        DBs are fresh; accounts are auto-created on first mint.
// ---------------------------------------------------------------------------
// 10 fixed, distinct account GUIDs, each a single repeated hex digit:
// 11111111-1111-1111-1111-111111111111 ... aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
const ACCOUNT_DIGITS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'a'];
const DEFAULT_ACCOUNTS = ACCOUNT_DIGITS.map((d) =>
  `${d.repeat(8)}-${d.repeat(4)}-${d.repeat(4)}-${d.repeat(4)}-${d.repeat(12)}`);

const GATEWAY   = mustEnv('GATEWAY_URL');
const KC        = mustEnv('KEYCLOAK_AUTHORITY');
const CLIENT_ID = __ENV.LEDGER_CLIENT_ID || 'ledger-service-client';
const SECRET    = mustEnv('LEDGER_CLIENT_SECRET');
const ASSET     = __ENV.ASSET || 'USD';
const API_VERSION = '1';

const ACCOUNTS = (__ENV.ACCOUNT_IDS
  ? __ENV.ACCOUNT_IDS.split(',').map((s) => s.trim()).filter(Boolean)
  : DEFAULT_ACCOUNTS);

// Bounded, predictable load: total requests = RATE * DURATION_SECONDS.
// Defaults: 50 req/s for 60s = 3000 mints. Override with env vars.
const RATE             = Number(__ENV.RATE || 50);              // requests per second
const DURATION_SECONDS = Number(__ENV.DURATION_SECONDS || 60);  // how long to sustain it

export const options = {
  scenarios: {
    mint: {
      executor: 'constant-arrival-rate',
      exec: 'mint',
      rate: RATE,
      timeUnit: '1s',
      duration: `${DURATION_SECONDS}s`,
      // Caps concurrency so a slow server can't be piled onto without bound:
      // if the rate can't be met, k6 stops sending rather than spawning more VUs.
      preAllocatedVUs: 50,
      maxVUs: 100,
      gracefulStop: '10s',
    },
  },
  thresholds: {
    'http_req_failed{phase:mint}':   ['rate<0.01'],
    'http_req_duration{phase:mint}': ['p(95)<1000'],
  },
};

function mustEnv(name) {
  const v = __ENV[name];
  if (!v) fail(`Missing required env var: ${name}`);
  return v;
}

export function setup() {
  const res = http.post(`${KC}/protocol/openid-connect/token`, {
    grant_type: 'client_credentials',
    client_id: CLIENT_ID,
    client_secret: SECRET,
    scope: 'ledger:write ledger:read',
  });
  check(res, { 'token 200': (r) => r.status === 200 }) ||
    fail(`token fetch failed: ${res.status} ${res.body}`);
  console.log(`minting across ${ACCOUNTS.length} accounts`);
  return { token: res.json('access_token') };
}

// Mint amount range (inclusive). Override with MINT_MIN / MINT_MAX env vars.
const MINT_MIN = Number(__ENV.MINT_MIN || 1);
const MINT_MAX = Number(__ENV.MINT_MAX || 1000);

export function mint(data) {
  // Spread credits evenly across the 10 accounts.
  const id = ACCOUNTS[Math.floor(Math.random() * ACCOUNTS.length)];
  // Vary the amount so balances grow unevenly, not a flat 1 USD each time.
  const amount = MINT_MIN + Math.floor(Math.random() * (MINT_MAX - MINT_MIN + 1));
  const res = http.post(
    `${GATEWAY}/mint`,
    JSON.stringify({ creditAccountId: id, amount, asset: ASSET }),
    {
      headers: {
        Authorization: `Bearer ${data.token}`,
        'Idempotency-Key': uuidv4(),
        'Content-Type': 'application/json',
        'api-version': API_VERSION,
      },
      tags: { phase: 'mint' },
    });
  check(res, { 'mint 202': (r) => r.status === 202 });
}

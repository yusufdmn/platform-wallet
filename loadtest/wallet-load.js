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
const ACCOUNT_COUNT = 10;

// 10 fixed, distinct account GUIDs (000..0001 through 000..0010).
const DEFAULT_ACCOUNTS = Array.from({ length: ACCOUNT_COUNT }, (_, i) =>
  `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`);

const GATEWAY   = mustEnv('GATEWAY_URL');
const KC        = mustEnv('KEYCLOAK_AUTHORITY');
const CLIENT_ID = __ENV.LEDGER_CLIENT_ID || 'ledger-service-client';
const SECRET    = mustEnv('LEDGER_CLIENT_SECRET');
const ASSET     = __ENV.ASSET || 'USD';
const API_VERSION = '1';

const ACCOUNTS = (__ENV.ACCOUNT_IDS
  ? __ENV.ACCOUNT_IDS.split(',').map((s) => s.trim()).filter(Boolean)
  : DEFAULT_ACCOUNTS);

export const options = {
  scenarios: {
    mint: {
      executor: 'ramping-vus',
      exec: 'mint',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 40 },
        { duration: '1m',  target: 40 },
        { duration: '20s', target: 0 },
      ],
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

export function mint(data) {
  // Spread credits evenly across the 10 accounts.
  const id = ACCOUNTS[Math.floor(Math.random() * ACCOUNTS.length)];
  const res = http.post(
    `${GATEWAY}/mint`,
    JSON.stringify({ creditAccountId: id, amount: 1, asset: ASSET }),
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

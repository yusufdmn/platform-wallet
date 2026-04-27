-- Bootstraps the four per-service databases. Run once at container init.
-- Each service owns its own database; no cross-database joins.
--
-- POSTGRES_PASSWORD env var is injected by the Docker entrypoint. We read it
-- here so the wallet role password stays in sync with .env — no hardcoded secret.

\getenv walletpw POSTGRES_PASSWORD

CREATE DATABASE intake_db;
CREATE DATABASE saga_db;
CREATE DATABASE ledger_db;
CREATE DATABASE webhook_db;

-- Shared app role used by every service for its own DB.
CREATE ROLE wallet WITH LOGIN PASSWORD :'walletpw';
GRANT ALL PRIVILEGES ON DATABASE intake_db  TO wallet;
GRANT ALL PRIVILEGES ON DATABASE saga_db    TO wallet;
GRANT ALL PRIVILEGES ON DATABASE ledger_db  TO wallet;
GRANT ALL PRIVILEGES ON DATABASE webhook_db TO wallet;

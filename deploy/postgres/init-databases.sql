-- Bootstraps the four per-service databases. Run once at container init.
-- Each service owns its own database; no cross-database joins.

CREATE DATABASE intake_db;
CREATE DATABASE saga_db;
CREATE DATABASE ledger_db;
CREATE DATABASE webhook_db;

-- Shared app role used by every service for its own DB. Fine-grained per-DB
-- ownership is applied later in migrations (see TheMainPlan.md §6.1).
CREATE ROLE wallet WITH LOGIN PASSWORD 'wallet';
GRANT ALL PRIVILEGES ON DATABASE intake_db  TO wallet;
GRANT ALL PRIVILEGES ON DATABASE saga_db    TO wallet;
GRANT ALL PRIVILEGES ON DATABASE ledger_db  TO wallet;
GRANT ALL PRIVILEGES ON DATABASE webhook_db TO wallet;

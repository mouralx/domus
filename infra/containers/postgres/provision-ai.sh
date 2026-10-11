#!/bin/sh
set -eu
# Can also be run against an existing volume; it never replaces existing passwords/data.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  --set=ai_password="${AI_DB_PASSWORD:-domus-ai-local-change-me}" <<'SQL'
SELECT format('CREATE ROLE domus_ai LOGIN PASSWORD %L', :'ai_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='domus_ai') \gexec
SELECT 'CREATE DATABASE ai OWNER domus_ai'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname='ai') \gexec
REVOKE CONNECT ON DATABASE ai FROM PUBLIC;
GRANT CONNECT ON DATABASE ai TO domus_ai;
SQL

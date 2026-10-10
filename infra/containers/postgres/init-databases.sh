#!/bin/sh
set -eu
# Quote through psql variables rather than interpolating SQL or passwords.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  --set=membership_password="$MEMBERSHIP_DB_PASSWORD" --set=boards_password="$BOARDS_DB_PASSWORD" --set=automations_password="$AUTOMATIONS_DB_PASSWORD" <<'SQL'
CREATE ROLE domus_membership LOGIN PASSWORD :'membership_password';
CREATE DATABASE membership OWNER domus_membership;
REVOKE CONNECT ON DATABASE membership FROM PUBLIC;
GRANT CONNECT ON DATABASE membership TO domus_membership;
CREATE ROLE domus_boards LOGIN PASSWORD :'boards_password';
CREATE ROLE domus_automations LOGIN PASSWORD :'automations_password';
CREATE DATABASE boards OWNER domus_boards;
CREATE DATABASE automations OWNER domus_automations;
REVOKE CONNECT ON DATABASE boards FROM PUBLIC;
REVOKE CONNECT ON DATABASE automations FROM PUBLIC;
GRANT CONNECT ON DATABASE boards TO domus_boards;
GRANT CONNECT ON DATABASE automations TO domus_automations;
SQL

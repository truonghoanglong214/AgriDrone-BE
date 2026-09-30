#!/usr/bin/env bash
# PostgreSQL executes this file inside a Linux container; keep LF line endings.
set -Eeuo pipefail

: "${BE1_DB_NAME:?BE1_DB_NAME is required}"
: "${BE1_DB_USERNAME:?BE1_DB_USERNAME is required}"
: "${BE1_DB_PASSWORD:?BE1_DB_PASSWORD is required}"
: "${BE2_DB_NAME:?BE2_DB_NAME is required}"
: "${BE2_DB_USERNAME:?BE2_DB_USERNAME is required}"
: "${BE2_DB_PASSWORD:?BE2_DB_PASSWORD is required}"

psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set be1_db="$BE1_DB_NAME" --set be1_user="$BE1_DB_USERNAME" --set be1_password="$BE1_DB_PASSWORD" \
  --set be2_db="$BE2_DB_NAME" --set be2_user="$BE2_DB_USERNAME" --set be2_password="$BE2_DB_PASSWORD" <<-'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'be1_user', :'be1_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'be1_user') \gexec
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'be2_user', :'be2_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'be2_user') \gexec

SELECT format('CREATE DATABASE %I OWNER %I', :'be1_db', :'be1_user')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'be1_db') \gexec
SELECT format('CREATE DATABASE %I OWNER %I', :'be2_db', :'be2_user')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'be2_db') \gexec

SELECT format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', :'be1_db') \gexec
SELECT format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', :'be2_db') \gexec
SELECT format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO %I', :'be1_db', :'be1_user') \gexec
SELECT format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO %I', :'be2_db', :'be2_user') \gexec
SQL

# PostGIS is not a trusted extension. Install database prerequisites while the
# bootstrap superuser is still active; Flyway's IF NOT EXISTS declarations then
# document and validate them while running as the non-superuser BE1 owner.
psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$BE1_DB_NAME" <<-'SQL'
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS postgis;
SQL

psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$BE2_DB_NAME" <<-'SQL'
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS postgis;
SQL

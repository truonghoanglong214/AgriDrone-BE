#!/usr/bin/env bash
# ==============================================================================
# AgriDrone — Database Restore Drill Script
# Usage: ./restore.sh <dump_file_path> <target_database_name>
# Safety: NEVER overwrite the active database directly without explicit target!
# ==============================================================================

set -euo pipefail

BASE_DIR="/opt/agridrone"
ENV_FILE="${BASE_DIR}/secrets/.env.production"

if [ $# -lt 2 ]; then
    echo "Usage: $0 <path_to_dump_file> <target_database_name>"
    echo "Example: $0 /opt/agridrone/backups/agridrone_pre-deploy_20261119.dump agridrone_drill"
    exit 1
fi

DUMP_PATH="$1"
TARGET_DB="$2"

if [ ! -f "${DUMP_PATH}" ]; then
    echo "ERROR: Dump file not found: ${DUMP_PATH}" >&2
    exit 1
fi

# Verify checksum if available
if [ -f "${DUMP_PATH}.sha256" ]; then
    EXPECTED_HASH="$(cat "${DUMP_PATH}.sha256")"
    ACTUAL_HASH="$(sha256sum "${DUMP_PATH}" | awk '{print $1}')"
    if [ "${EXPECTED_HASH}" != "${ACTUAL_HASH}" ]; then
        echo "ERROR: Checksum mismatch! Expected ${EXPECTED_HASH}, got ${ACTUAL_HASH}" >&2
        exit 1
    fi
    echo "Checksum verified: OK"
fi

POSTGRES_USER="$(grep '^POSTGRES_USER=' "${ENV_FILE}" | cut -d '=' -f2-)"
ACTIVE_DB="$(grep '^POSTGRES_DB=' "${ENV_FILE}" | cut -d '=' -f2-)"

if [ "${TARGET_DB}" = "${ACTIVE_DB}" ]; then
    echo "WARNING: Target database is the LIVE production database (${ACTIVE_DB})!"
    read -rp "Are you absolutely sure you want to OVERWRITE the live database? (type 'YES-OVERWRITE'): " CONFIRM
    if [ "${CONFIRM}" != "YES-OVERWRITE" ]; then
        echo "Restore cancelled."
        exit 0
    fi
fi

echo "===> Creating target database '${TARGET_DB}' if not exists..."
docker compose --env-file "${ENV_FILE}" -f "${BASE_DIR}/compose.production.yaml" \
    exec -T postgres psql -U "${POSTGRES_USER}" -d postgres -c "CREATE DATABASE \"${TARGET_DB}\";" 2>/dev/null || true

echo "===> Restoring dump into '${TARGET_DB}'..."
docker compose --env-file "${ENV_FILE}" -f "${BASE_DIR}/compose.production.yaml" \
    exec -T postgres pg_restore -U "${POSTGRES_USER}" -d "${TARGET_DB}" --clean --if-exists --no-owner < "${DUMP_PATH}" || true

echo "===> Verifying PostGIS and Migrations in '${TARGET_DB}'..."
docker compose --env-file "${ENV_FILE}" -f "${BASE_DIR}/compose.production.yaml" \
    exec -T postgres psql -U "${POSTGRES_USER}" -d "${TARGET_DB}" -c "SELECT PostGIS_Version(); SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"

echo "Restore drill completed successfully!"

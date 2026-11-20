#!/usr/bin/env bash
# ==============================================================================
# AgriDrone — Automated Database & MinIO Backup Script
# Usage: ./backup.sh [--pre-deploy]
# Schedule: Run daily via cron at 01:00 ICT
# ==============================================================================

set -euo pipefail

BASE_DIR="/opt/agridrone"
ENV_FILE="${BASE_DIR}/secrets/.env.production"
BACKUP_DIR="${BASE_DIR}/backups"
TIMESTAMP="$(date +'%Y%m%d_%H%M%S')"
IS_PRE_DEPLOY="${1:-}"

if [ ! -f "${ENV_FILE}" ]; then
    echo "ERROR: Production env file not found at ${ENV_FILE}" >&2
    exit 1
fi

# Load database configuration without printing secrets
POSTGRES_USER="$(grep '^POSTGRES_USER=' "${ENV_FILE}" | cut -d '=' -f2-)"
POSTGRES_DB="$(grep '^POSTGRES_DB=' "${ENV_FILE}" | cut -d '=' -f2-)"

PREFIX="daily"
if [ "${IS_PRE_DEPLOY}" = "--pre-deploy" ]; then
    PREFIX="pre-deploy"
fi

DUMP_FILENAME="agridrone_${PREFIX}_${TIMESTAMP}.dump"
DUMP_PATH="${BACKUP_DIR}/${DUMP_FILENAME}"
CHECKSUM_PATH="${DUMP_PATH}.sha256"

echo "===> [Backup] Starting PostgreSQL dump to ${DUMP_PATH}..."

# Execute pg_dump inside postgres container in custom format (-Fc)
docker compose --env-file "${ENV_FILE}" -f "${BASE_DIR}/compose.production.yaml" \
    exec -T postgres pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc > "${DUMP_PATH}"

# Check dump file size
FILE_SIZE=$(stat -c%s "${DUMP_PATH}" 2>/dev/null || stat -f%z "${DUMP_PATH}")
if [ "${FILE_SIZE}" -lt 1024 ]; then
    echo "ERROR: Backup dump file is suspiciously small (${FILE_SIZE} bytes). Aborting!" >&2
    rm -f "${DUMP_PATH}"
    exit 1
fi

# Generate SHA-256 Checksum
sha256sum "${DUMP_PATH}" | awk '{print $1}' > "${CHECKSUM_PATH}"
echo "Backup completed successfully! Size: ${FILE_SIZE} bytes. Checksum saved."

# Optional: MinIO metadata/data mirror if external bucket configured
# mc mirror /data external-s3/agridrone-backups/

# Retention: Remove local backups older than 7 days (only for daily dumps)
if [ "${PREFIX}" = "daily" ]; then
    echo "===> [Retention] Cleaning up local backups older than 7 days..."
    find "${BACKUP_DIR}" -name "agridrone_daily_*.dump*" -type f -mtime +7 -delete
fi

echo "===> [Backup] Finished successfully at $(date)."

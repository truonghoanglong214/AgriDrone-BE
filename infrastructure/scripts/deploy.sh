#!/usr/bin/env bash
# ==============================================================================
# AgriDrone — Automated Deployment Script with Lock, Pre-deploy Backup & Rollback
# Usage: ./deploy.sh <GIT_SHA_OR_TAG>
# Example: ./deploy.sh c04d3e5
# ==============================================================================

set -euo pipefail

BASE_DIR="/opt/agridrone"
LOCK_FILE="/tmp/agridrone-deploy.lock"
ENV_FILE="${BASE_DIR}/secrets/.env.production"
CURRENT_RELEASE_FILE="${BASE_DIR}/releases/current_release.txt"
PREVIOUS_RELEASE_FILE="${BASE_DIR}/releases/previous_release.txt"

if [ $# -lt 1 ]; then
    echo "Usage: $0 <GIT_SHA_OR_IMAGE_TAG> [REPO_NAME]"
    exit 1
fi

TARGET_TAG="$1"
REPO_NAME="${2:-truonghoanglong214/agridrone-be}"

# 1. Acquire Deployment Lock (Prevent concurrent deployments)
exec 200>"${LOCK_FILE}"
if ! flock -n 200; then
    echo "ERROR: Another deployment is currently in progress! Aborting." >&2
    exit 1
fi

echo "========================================================================"
echo "AgriDrone Deploy: Deploying Release [${TARGET_TAG}] from [${REPO_NAME}] at $(date)"
echo "========================================================================"

# Save current release for rollback reference if available
PREVIOUS_TAG=""
if [ -f "${CURRENT_RELEASE_FILE}" ]; then
    PREVIOUS_TAG="$(cat "${CURRENT_RELEASE_FILE}")"
    echo "${PREVIOUS_TAG}" > "${PREVIOUS_RELEASE_FILE}"
fi

cd "${BASE_DIR}"

# Sync latest configs from local git cache if available
if [ -d "/tmp/agridrone-repo/.git" ]; then
    echo "===> Syncing latest configs from /tmp/agridrone-repo..."
    git -C /tmp/agridrone-repo pull origin main || true
    cp /tmp/agridrone-repo/compose.production.yaml "${BASE_DIR}/compose.production.yaml" || true
    if [ -f "/tmp/agridrone-repo/infrastructure/caddy/Caddyfile" ]; then
        mkdir -p "${BASE_DIR}/infrastructure/caddy"
        cp /tmp/agridrone-repo/infrastructure/caddy/Caddyfile "${BASE_DIR}/infrastructure/caddy/Caddyfile" || true
    fi
    if [ -d "/tmp/agridrone-repo/infrastructure/scripts" ]; then
        cp /tmp/agridrone-repo/infrastructure/scripts/*.sh "${BASE_DIR}/scripts/" || true
        chmod +x "${BASE_DIR}/scripts/"*.sh || true
    fi
fi

# 2. Pre-deploy Safety Backup
echo "===> [1/6] Running pre-deployment database backup..."
if [ -f "${BASE_DIR}/scripts/backup.sh" ]; then
    bash "${BASE_DIR}/scripts/backup.sh" --pre-deploy
fi

# 3. Pull new images from GHCR
echo "===> [2/6] Pulling images for release ${TARGET_TAG}..."
export API_IMAGE="ghcr.io/${REPO_NAME}/agridrone-api:${TARGET_TAG}"
export MIGRATOR_IMAGE="ghcr.io/${REPO_NAME}/agridrone-migrator:${TARGET_TAG}"

docker pull "${API_IMAGE}" || echo "Warning: API image pull failed or using local build."
docker pull "${MIGRATOR_IMAGE}" || echo "Warning: Migrator image pull failed or using local build."

# 4. Start Core Data Services
echo "===> [3/6] Starting core data services (Postgres, RabbitMQ, Redis, MinIO)..."
docker compose --env-file "${ENV_FILE}" -f compose.production.yaml up -d postgres rabbitmq redis minio

# Run MinIO initialization (Private bucket setup; waits for minio healthy automatically via depends_on)
docker compose --env-file "${ENV_FILE}" -f compose.production.yaml run --rm minio-init

# 5. One-Shot Database Migration
echo "===> [4/6] Executing database migration one-shot job..."
if ! docker compose --env-file "${ENV_FILE}" -f compose.production.yaml run --rm migrate; then
    echo "CRITICAL ERROR: Database migration failed! Aborting deployment." >&2
    exit 1
fi
echo "Migration succeeded!"

# 6. Deploy / Update API and Reverse Proxy
echo "===> [5/6] Updating API and Caddy reverse proxy..."
docker compose --env-file "${ENV_FILE}" -f compose.production.yaml up -d --no-deps api caddy

# 7. Post-Deployment Smoke Test
echo "===> [6/6] Running automated post-deployment smoke test..."
sleep 5

if ! bash "${BASE_DIR}/scripts/smoke-test.sh"; then
    echo "========================================================================"
    echo "WARNING: Smoke test failed! Initiating rollback..."
    echo "========================================================================"
    if [ -n "${PREVIOUS_TAG}" ]; then
        echo "Rolling back to previous known-good release: ${PREVIOUS_TAG}"
        export API_IMAGE="ghcr.io/${REPO_NAME}/agridrone-api:${PREVIOUS_TAG}"
        docker compose --env-file "${ENV_FILE}" -f compose.production.yaml up -d --no-deps api
        echo "Rollback completed. Please inspect logs!"
    else
        echo "No previous release recorded for rollback."
    fi
    exit 1
fi

# Record current active release
echo "${TARGET_TAG}" > "${CURRENT_RELEASE_FILE}"
echo "========================================================================"
echo "DEPLOYMENT SUCCESSFUL: Release ${TARGET_TAG} is now active!"
echo "========================================================================"

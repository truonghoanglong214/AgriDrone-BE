#!/usr/bin/env bash
# ==============================================================================
# AgriDrone — Production Post-Deployment Smoke Test Script
# Usage: ./smoke-test.sh [app_domain]
# ==============================================================================

set -euo pipefail

BASE_DIR="/opt/agridrone"
ENV_FILE="${BASE_DIR}/secrets/.env.production"

APP_DOMAIN="${1:-$(grep '^APP_DOMAIN=' "${ENV_FILE}" 2>/dev/null | cut -d '=' -f2- || echo 'localhost')}"
API_BASE="https://${APP_DOMAIN}"

echo "========================================================================"
echo "Starting AgriDrone Smoke Test for domain: ${APP_DOMAIN}"
echo "========================================================================"

FAILED=0

check_endpoint() {
    local name="$1"
    local url="$2"
    local expected_status="$3"

    echo -n "Checking ${name} (${url})... "
    HTTP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" --connect-timeout 10 "${url}" || echo "000")
    if [ "${HTTP_CODE}" = "${expected_status}" ]; then
        echo "PASS (HTTP ${HTTP_CODE})"
    else
        echo "FAIL (Expected ${expected_status}, got ${HTTP_CODE})"
        FAILED=$((FAILED + 1))
    fi
}

# 1. Container health checks
echo "===> [1/4] Checking Docker Container Health..."
cd "${BASE_DIR}"
UNHEALTHY=$(docker compose -f compose.production.yaml ps --filter "health=unhealthy" -q | wc -l)
if [ "${UNHEALTHY}" -gt 0 ]; then
    echo "ERROR: Found ${UNHEALTHY} unhealthy container(s)!"
    docker compose -f compose.production.yaml ps
    FAILED=$((FAILED + 1))
else
    echo "All active compose services are healthy/running."
fi

# 2. Public Endpoints via Caddy (HTTPS)
echo "===> [2/4] Testing API Health Endpoints..."
check_endpoint "API Liveness" "${API_BASE}/health/live" "200"
check_endpoint "API Readiness" "${API_BASE}/health/ready" "200"
check_endpoint "MinIO Storage Health" "${API_BASE}/health/minio" "200"

# 3. Security: Check that internal data ports are not exposed on public interface
echo "===> [3/4] Verifying Public Port Isolation..."
for PORT in 5432 5672 15672 6379 9001 8000 3000; do
    if nc -z -w 2 127.0.0.1 "${PORT}" 2>/dev/null; then
        echo "WARNING: Port ${PORT} is open on host! Check if it is accessible externally."
    else
        echo "Port ${PORT} is NOT exposed on host loopback. (Secure)"
    fi
done

# 4. Safety flags check
echo "===> [4/4] Verifying Business Safety Flags..."
if grep -q "SurveyPayments__Enabled=true" "${ENV_FILE}"; then
    echo "CRITICAL ERROR: SurveyPayments__Enabled is set to true before Phase V3-10!"
    FAILED=$((FAILED + 1))
else
    echo "Safety Gate: SurveyPayments is FAIL-CLOSED (false). OK."
fi

echo "========================================================================"
if [ "${FAILED}" -eq 0 ]; then
    echo "SMOKE TEST RESULT: ALL CHECKS PASSED!"
    exit 0
else
    echo "SMOKE TEST RESULT: ${FAILED} CHECK(S) FAILED!"
    exit 1
fi

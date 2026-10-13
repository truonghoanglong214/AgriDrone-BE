/*
  Be-Plan Phase 0 legacy-data inventory.

  Safety contract:
  - Run with psql against a restored production-like snapshot.
  - The transaction is READ ONLY and this file contains no DDL/DML.
  - Save the complete output with the database snapshot identifier and review it
    before any Phase 2/3/8/11 backfill or schema removal.
*/

BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '5min';
SET LOCAL lock_timeout = '5s';

SELECT
    current_database() AS database_name,
    current_user AS executed_by,
    CURRENT_TIMESTAMP AS captured_at,
    txid_current_snapshot() AS transaction_snapshot;

-- 1. Legacy TenantAdmin/Member memberships. OWNER remains customer data.
SELECT
    tm.role::text AS legacy_role,
    tm.status::text AS status,
    COUNT(*) AS record_count,
    COUNT(DISTINCT tm.tenant_id) AS tenant_count,
    COUNT(DISTINCT tm.user_id) AS user_count,
    'Identity/Product'::text AS remediation_owner,
    'Review account intent; do not auto-convert to TenantOwner or SystemManager.'::text
        AS recommended_disposition
FROM identity.tenant_memberships AS tm
WHERE tm.role::text IN ('TENANT_ADMIN', 'MEMBER')
GROUP BY tm.role, tm.status
ORDER BY tm.role::text, tm.status::text;

SELECT
    tm.id,
    tm.tenant_id,
    tm.user_id,
    u.email,
    tm.role::text AS legacy_role,
    tm.status::text AS status,
    tm.joined_at,
    'Identity/Product'::text AS remediation_owner
FROM identity.tenant_memberships AS tm
JOIN identity.users AS u ON u.id = tm.user_id
WHERE tm.role::text IN ('TENANT_ADMIN', 'MEMBER')
ORDER BY tm.tenant_id, tm.role::text, u.email;

-- 2. Legacy Farm Manager/Worker and their Zone assignments.
SELECT
    fm.role::text AS legacy_role,
    fm.status::text AS status,
    fm.access_scope::text AS access_scope,
    COUNT(*) AS membership_count,
    COUNT(DISTINCT fm.farm_id) AS farm_count,
    COUNT(za.id) FILTER (WHERE za.revoked_at IS NULL) AS active_zone_assignment_count,
    'Operations/SystemAdmin'::text AS remediation_owner,
    'Validate identity, flight qualification and availability before any SystemManager profile/assignment.'::text
        AS recommended_disposition
FROM identity.farm_memberships AS fm
LEFT JOIN identity.zone_assignments AS za
    ON za.farm_membership_id = fm.id
GROUP BY fm.role, fm.status, fm.access_scope
ORDER BY fm.role::text, fm.status::text, fm.access_scope::text;

SELECT
    fm.id AS farm_membership_id,
    fm.tenant_id,
    fm.farm_id,
    fm.user_id,
    u.email,
    fm.role::text AS legacy_role,
    fm.status::text AS status,
    fm.access_scope::text AS access_scope,
    za.id AS zone_assignment_id,
    za.zone_id,
    za.assigned_at,
    za.revoked_at,
    'Operations/SystemAdmin'::text AS remediation_owner
FROM identity.farm_memberships AS fm
JOIN identity.users AS u ON u.id = fm.user_id
LEFT JOIN identity.zone_assignments AS za
    ON za.farm_membership_id = fm.id
ORDER BY fm.farm_id, fm.user_id, za.zone_id;

-- 3. Tenant-owned Drone inventory and global identifier collisions.
SELECT
    d.tenant_id,
    d.status::text AS status,
    COUNT(*) AS drone_count,
    COUNT(*) FILTER (WHERE d.deleted_at IS NULL) AS non_deleted_count,
    'Fleet/SystemAdmin'::text AS remediation_owner
FROM mission.drones AS d
GROUP BY d.tenant_id, d.status
ORDER BY d.tenant_id, d.status::text;

WITH identifiers AS (
    SELECT id, tenant_id, 'CODE'::text AS identifier_type,
           UPPER(BTRIM(code)) AS normalized_value
    FROM mission.drones
    WHERE deleted_at IS NULL
    UNION ALL
    SELECT id, tenant_id, 'SERIAL_NUMBER', UPPER(BTRIM(serial_number))
    FROM mission.drones
    WHERE deleted_at IS NULL AND NULLIF(BTRIM(serial_number), '') IS NOT NULL
    UNION ALL
    SELECT id, tenant_id, 'REGISTRATION_NUMBER', UPPER(BTRIM(registration_number))
    FROM mission.drones
    WHERE deleted_at IS NULL AND NULLIF(BTRIM(registration_number), '') IS NOT NULL
)
SELECT
    identifier_type,
    normalized_value,
    COUNT(*) AS collision_count,
    COUNT(DISTINCT tenant_id) AS tenant_count,
    ARRAY_AGG(id ORDER BY id) AS drone_ids,
    ARRAY_AGG(DISTINCT tenant_id ORDER BY tenant_id) AS tenant_ids,
    'Fleet/SystemAdmin'::text AS remediation_owner,
    'Resolve manually; never merge two physical drones automatically.'::text
        AS recommended_disposition
FROM identifiers
GROUP BY identifier_type, normalized_value
HAVING COUNT(*) > 1
ORDER BY identifier_type, normalized_value;

-- 4. Mission inventory before SurveyOrder exists.
-- Phase-0 schema has no survey_order_id column; consequently every current
-- mission requires reconciliation to a future order or an explicit legacy-only disposition.
SELECT
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'mission'
          AND table_name = 'drone_missions'
          AND column_name = 'survey_order_id'
    ) AS survey_order_column_exists,
    COUNT(*) AS missions_without_survey_order_in_phase0_schema,
    COUNT(DISTINCT tenant_id) AS tenant_count,
    COUNT(DISTINCT farm_id) AS farm_count,
    'Operations/Product'::text AS remediation_owner,
    'Match to an approved request/order using evidence, or retain as legacy history; do not infer silently.'::text
        AS recommended_disposition
FROM mission.drone_missions;

SELECT
    id AS mission_id,
    tenant_id,
    farm_id,
    zone_id,
    drone_id,
    mission_code,
    mission_type::text AS mission_type,
    status::text AS status,
    created_at,
    'Operations/Product'::text AS remediation_owner
FROM mission.drone_missions
ORDER BY created_at, id;

-- 5. Current ZoneMapVersion slices grouped by Farm.
SELECT
    f.id AS farm_id,
    f.tenant_id,
    COUNT(z.id) AS zone_count,
    COUNT(zmv.id) FILTER (WHERE zmv.status::text = 'CONFIRMED') AS confirmed_slice_count,
    COUNT(DISTINCT zmv.source_mission_id)
        FILTER (WHERE zmv.status::text = 'CONFIRMED') AS source_mission_count,
    MIN(zmv.confirmed_at) FILTER (WHERE zmv.status::text = 'CONFIRMED')
        AS earliest_confirmed_at,
    MAX(zmv.confirmed_at) FILTER (WHERE zmv.status::text = 'CONFIRMED')
        AS latest_confirmed_at,
    'GIS/Operations'::text AS remediation_owner,
    'Validate compatible slices before grouping under a FarmBaseMapVersion.'::text
        AS recommended_disposition
FROM farm.farms AS f
LEFT JOIN farm.farm_zones AS z ON z.farm_id = f.id
LEFT JOIN farm.zone_map_versions AS zmv
    ON zmv.zone_id = z.id
   AND zmv.status::text = 'CONFIRMED'
GROUP BY f.id, f.tenant_id
ORDER BY f.id;

SELECT
    zmv.id AS zone_map_version_id,
    zmv.farm_id,
    zmv.zone_id,
    zmv.version_number,
    zmv.source_mission_id,
    zmv.source_approval_id,
    zmv.confirmed_by,
    zmv.confirmed_at,
    'GIS/Operations'::text AS remediation_owner
FROM farm.zone_map_versions AS zmv
WHERE zmv.status::text = 'CONFIRMED'
ORDER BY zmv.farm_id, zmv.zone_id;

-- 6. Out-of-scope FieldTask and Harvest data retained for history/retention review.
SELECT
    'field_task.field_tasks'::text AS relation_name,
    COUNT(*) AS record_count,
    COUNT(DISTINCT farm_id) AS farm_count,
    'Data Governance/Product'::text AS remediation_owner,
    'Retain read-only until Phase 11 retention approval.'::text AS recommended_disposition
FROM field_task.field_tasks
UNION ALL
SELECT
    'harvest.harvest_batches', COUNT(*), COUNT(DISTINCT farm_id),
    'Data Governance/Product',
    'Retain read-only until Phase 11 retention approval.'
FROM harvest.harvest_batches
UNION ALL
SELECT
    'harvest.plant_harvest_records', COUNT(*), COUNT(DISTINCT farm_id),
    'Data Governance/Product',
    'Retain read-only until Phase 11 retention approval.'
FROM harvest.plant_harvest_records
ORDER BY relation_name;

-- A successful COMMIT proves the script completed without writes.
COMMIT;

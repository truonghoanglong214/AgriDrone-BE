/*
  Be-Plan Phase 4 legacy inventory.

  Run this script against the approved production-like snapshot before any
  contract migration drops legacy tables or enum types. It is read-only and
  emits aggregate counts/checksums without email addresses or user IDs.
*/

BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '5min';
SET LOCAL lock_timeout = '5s';

SELECT
    current_database() AS database_name,
    CURRENT_TIMESTAMP AS captured_at,
    txid_current_snapshot() AS transaction_snapshot;

SELECT
    'field_task.field_tasks' AS relation_name,
    COUNT(*) AS record_count,
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), '')) AS id_checksum
FROM field_task.field_tasks
UNION ALL
SELECT
    'field_task.task_assignments',
    COUNT(*),
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), ''))
FROM field_task.task_assignments
UNION ALL
SELECT
    'field_task.task_updates',
    COUNT(*),
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), ''))
FROM field_task.task_updates
UNION ALL
SELECT
    'field_task.task_media',
    COUNT(*),
    MD5(COALESCE(
        STRING_AGG(task_id::text || ':' || media_id::text, ','
            ORDER BY task_id, media_id),
        ''))
FROM field_task.task_media
UNION ALL
SELECT
    'harvest.seasons',
    COUNT(*),
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), ''))
FROM harvest.seasons
UNION ALL
SELECT
    'harvest.harvest_batches',
    COUNT(*),
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), ''))
FROM harvest.harvest_batches
UNION ALL
SELECT
    'harvest.plant_harvest_records',
    COUNT(*),
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), ''))
FROM harvest.plant_harvest_records
UNION ALL
SELECT
    'harvest.harvest_quality_grades',
    COUNT(*),
    MD5(COALESCE(STRING_AGG(id::text, ',' ORDER BY id), ''))
FROM harvest.harvest_quality_grades
UNION ALL
SELECT
    'harvest.plant_harvest_quality_details',
    COUNT(*),
    MD5(COALESCE(
        STRING_AGG(
            plant_harvest_record_id::text || ':' || quality_grade_id::text,
            ',' ORDER BY plant_harvest_record_id, quality_grade_id),
        ''))
FROM harvest.plant_harvest_quality_details
ORDER BY relation_name;

SELECT
    tm.role::text AS legacy_role,
    tm.status::text AS status,
    COUNT(*) AS record_count,
    COUNT(DISTINCT tm.tenant_id) AS tenant_count
FROM identity.tenant_memberships AS tm
WHERE tm.role::text IN ('TENANT_ADMIN', 'MEMBER')
GROUP BY tm.role, tm.status
ORDER BY tm.role::text, tm.status::text;

SELECT
    fm.role::text AS legacy_role,
    fm.status::text AS status,
    COUNT(*) AS membership_count,
    COUNT(za.id) FILTER (WHERE za.revoked_at IS NULL)
        AS active_zone_assignment_count
FROM identity.farm_memberships AS fm
LEFT JOIN identity.zone_assignments AS za
    ON za.farm_membership_id = fm.id
GROUP BY fm.role, fm.status
ORDER BY fm.role::text, fm.status::text;

COMMIT;

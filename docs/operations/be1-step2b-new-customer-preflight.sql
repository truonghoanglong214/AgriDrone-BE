-- BE1 Step 2B preflight. Both result counts must be zero before migration.

SELECT COUNT(*) AS zero_estimated_pole_count_rows
FROM survey.survey_requests
WHERE estimated_pole_count = 0;

-- Existing messaging rows are expected to remain tenant-scoped. New NULL rows
-- are permitted only for notification.email-requested.v1 after the migration.
SELECT COUNT(*) AS existing_tenantless_messaging_rows
FROM (
    SELECT tenant_id, event_type FROM system.outbox_messages
    UNION ALL
    SELECT tenant_id, event_type FROM system.inbox_messages
) AS messages
WHERE tenant_id IS NULL;

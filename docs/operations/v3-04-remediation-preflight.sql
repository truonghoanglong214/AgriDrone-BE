-- V3-04 read-only operational preflight.
-- Run this file against local dev, shared dev, staging, and every database
-- that has ever used the LongTH/V3-04 migration chain. Do not modify history.

SELECT
    current_database() AS database_name,
    current_user AS database_user,
    current_setting('server_version') AS postgres_version,
    now() AS observed_at;

SELECT "MigrationId"
FROM system.__ef_migrations_history
WHERE "MigrationId" IN (
    '20261116131222_V3_04MappingAndPlantIdentity',
    '20261116145210_V3_04MappingLineageAndEvidenceRemediation',
    '20261117050302_V3_04CanonicalRemediationFinal'
)
ORDER BY "MigrationId";

SELECT
    event_type,
    status,
    COUNT(*) AS message_count
FROM system.outbox_messages
WHERE event_type = 'mapping.baseline-candidates-ready.v3'
GROUP BY event_type, status
ORDER BY status;

SELECT
    consumer_name,
    event_type,
    status,
    COUNT(*) AS message_count
FROM system.inbox_messages
WHERE event_type = 'mapping.baseline-candidates-ready.v3'
GROUP BY consumer_name, event_type, status
ORDER BY consumer_name, status;

SELECT COUNT(*) AS unresolved_v3_outbox_count
FROM system.outbox_messages
WHERE event_type = 'mapping.baseline-candidates-ready.v3'
  AND status <> 'PUBLISHED';

SELECT COUNT(*) AS unresolved_v3_inbox_count
FROM system.inbox_messages
WHERE event_type = 'mapping.baseline-candidates-ready.v3'
  AND status <> 'COMPLETED';

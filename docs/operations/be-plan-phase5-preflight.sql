-- BE1 / Be-Plan Phase 5 database-foundation preflight (read-only).
-- Run against the Phase 4 production-like snapshot before applying
-- 20260925100711_Phase5SurveyDatabaseFoundation.

BEGIN TRANSACTION READ ONLY;

SELECT "MigrationId", "ProductVersion"
FROM system.__ef_migrations_history
ORDER BY "MigrationId" DESC
LIMIT 5;

SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'survey'
   OR table_name IN (
       'farm_base_map_versions',
       'preflight_checklist_definitions',
       'mission_preflight_checklists')
ORDER BY table_schema, table_name;

SELECT
    count(*) FILTER (WHERE survey_order_id IS NULL) AS legacy_missions_without_order,
    count(*) AS total_missions
FROM mission.drone_missions;

SELECT
    count(*) FILTER (WHERE farm_base_map_version_id IS NULL) AS legacy_zone_maps_without_farm_header,
    count(*) AS total_zone_map_versions
FROM farm.zone_map_versions;

SELECT extname, extversion
FROM pg_extension
WHERE extname IN ('btree_gist', 'citext', 'pgcrypto', 'postgis')
ORDER BY extname;

ROLLBACK;

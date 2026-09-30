-- Read-only inventory for BE1 Java Migration Phase 0.
-- Run after applying the final mixed EF migration chain to a fresh PostgreSQL/PostGIS database:
--   psql "$AGRIDRONE_DB_CONNECTION" -X -v ON_ERROR_STOP=1 -f this-file.sql
--
-- The result contains every application table, constraint and index plus its cutover disposition.
-- PostgreSQL/PostGIS extension-owned schemas (public spatial_ref_sys, tiger, topology) are excluded.

WITH relation_disposition AS (
    SELECT
        relation.oid,
        namespace.nspname AS schema_name,
        relation.relname AS table_name,
        CASE
            WHEN namespace.nspname = 'mission'
                 AND relation.relname = 'drone_legacy_tenant_ownership'
                THEN 'DROP_LEGACY'
            WHEN namespace.nspname IN ('mission', 'media')
                THEN 'BE2_KEEP'
            WHEN namespace.nspname IN ('field_task', 'harvest')
                THEN 'DROP_LEGACY'
            WHEN namespace.nspname = 'identity'
                 AND relation.relname IN ('farm_memberships', 'zone_assignments')
                THEN 'DROP_LEGACY'
            WHEN namespace.nspname = 'identity'
                 AND relation.relname = 'tenant_memberships'
                THEN 'BE1_REDESIGN_OWNER_ONLY'
            WHEN namespace.nspname = 'plant'
                 AND relation.relname IN (
                     'plant_scans',
                     'plant_scan_media',
                     'scan_verifications',
                     'condition_detections',
                     'condition_lesions',
                     'condition_detection_reviews')
                THEN 'MOVE_RAW_FLOW_TO_BE2'
            WHEN namespace.nspname = 'system'
                 AND relation.relname = '__ef_migrations_history'
                THEN 'REPLACE_BY_FLYWAY'
            WHEN namespace.nspname IN (
                'identity', 'farm', 'plant', 'survey', 'notification', 'system')
                THEN 'BE1_KEEP'
            ELSE 'REVIEW'
        END AS disposition,
        CASE
            WHEN namespace.nspname = 'mission'
                 AND relation.relname = 'drone_legacy_tenant_ownership'
                THEN 'Temporary compatibility evidence; exclude from the fresh BE2 baseline.'
            WHEN namespace.nspname IN ('mission', 'media')
                THEN 'BE2 owns Drone, Mission, media, telemetry and AI operations.'
            WHEN namespace.nspname IN ('field_task', 'harvest')
                THEN 'Out-of-scope legacy capability; do not port to either active baseline.'
            WHEN namespace.nspname = 'identity'
                 AND relation.relname IN ('farm_memberships', 'zone_assignments')
                THEN 'Legacy Farm Manager/Worker authorization is not part of the target actor model.'
            WHEN namespace.nspname = 'identity'
                 AND relation.relname = 'tenant_memberships'
                THEN 'Keep TenantOwner selection/ownership only; remove TenantAdmin/Member semantics.'
            WHEN namespace.nspname = 'plant'
                 AND relation.relname IN (
                     'plant_scans',
                     'plant_scan_media',
                     'scan_verifications',
                     'condition_detections',
                     'condition_lesions',
                     'condition_detection_reviews')
                THEN 'Raw scan/detection processing belongs to BE2; BE1 stores official Survey results.'
            WHEN namespace.nspname = 'system'
                 AND relation.relname = '__ef_migrations_history'
                THEN 'Flyway is the sole BE1 migration owner.'
            WHEN namespace.nspname IN (
                'identity', 'farm', 'plant', 'survey', 'notification', 'system')
                THEN 'Target BE1 capability; recreate from the Java-owned Flyway baseline.'
            ELSE 'No automatic decision.'
        END AS rationale
    FROM pg_class AS relation
    JOIN pg_namespace AS namespace ON namespace.oid = relation.relnamespace
    WHERE relation.relkind IN ('r', 'p')
      AND namespace.nspname NOT IN (
          'pg_catalog',
          'information_schema',
          'public',
          'tiger',
          'tiger_data',
          'topology')
), inventory AS (
    SELECT
        'TABLE'::text AS object_type,
        source.schema_name,
        source.table_name,
        source.table_name AS object_name,
        source.disposition,
        source.rationale
    FROM relation_disposition AS source

    UNION ALL

    SELECT
        CASE constraint_record.contype
            WHEN 'p' THEN 'CONSTRAINT_PRIMARY_KEY'
            WHEN 'u' THEN 'CONSTRAINT_UNIQUE'
            WHEN 'f' THEN 'CONSTRAINT_FOREIGN_KEY'
            WHEN 'c' THEN 'CONSTRAINT_CHECK'
            WHEN 'x' THEN 'CONSTRAINT_EXCLUSION'
            ELSE 'CONSTRAINT_' || constraint_record.contype::text
        END,
        source.schema_name,
        source.table_name,
        constraint_record.conname,
        CASE
            WHEN constraint_record.contype = 'f'
                 AND target.oid IS NOT NULL
                 AND (
                     (source.disposition LIKE 'BE1%' AND target.disposition LIKE 'BE2%')
                     OR (source.disposition LIKE 'BE2%' AND target.disposition LIKE 'BE1%')
                     OR (source.disposition = 'BE1_KEEP' AND target.disposition = 'MOVE_RAW_FLOW_TO_BE2')
                     OR (source.disposition = 'MOVE_RAW_FLOW_TO_BE2' AND target.disposition = 'BE1_KEEP'))
                THEN 'DROP_CROSS_SERVICE_FK'
            ELSE source.disposition
        END,
        CASE
            WHEN constraint_record.contype = 'f' AND target.oid IS NOT NULL
                THEN source.rationale || ' References '
                    || target.schema_name || '.' || target.table_name || '.'
            ELSE source.rationale
        END
    FROM pg_constraint AS constraint_record
    JOIN relation_disposition AS source
        ON source.oid = constraint_record.conrelid
    LEFT JOIN relation_disposition AS target
        ON target.oid = constraint_record.confrelid

    UNION ALL

    SELECT
        'INDEX',
        source.schema_name,
        source.table_name,
        index_record.relname,
        source.disposition,
        source.rationale
    FROM pg_index AS index_metadata
    JOIN relation_disposition AS source
        ON source.oid = index_metadata.indrelid
    JOIN pg_class AS index_record
        ON index_record.oid = index_metadata.indexrelid
)
SELECT
    object_type,
    schema_name,
    table_name,
    object_name,
    disposition,
    rationale
FROM inventory
ORDER BY schema_name, table_name, object_type, object_name;

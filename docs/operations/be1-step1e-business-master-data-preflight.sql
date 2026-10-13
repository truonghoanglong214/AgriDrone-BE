-- BE1 Step 1E business-master-data migration preflight (read-only).
-- Run against the target database before applying
-- 20261009140000_AddVersionedBusinessMasterData.

SELECT "MigrationId", "ProductVersion"
FROM system.__ef_migrations_history
ORDER BY "MigrationId" DESC
LIMIT 10;

SELECT extname
FROM pg_extension
WHERE extname = 'btree_gist';

SELECT table_schema, table_name
FROM information_schema.tables
WHERE (table_schema, table_name) IN (
    ('survey', 'harvest_readiness_assessments'),
    ('mission', 'ai_processing_jobs'),
    ('mission', 'ai_model_versions'),
    ('mission', 'ai_threshold_profiles'),
    ('plant', 'health_levels'),
    ('plant', 'plant_conditions')
)
ORDER BY table_schema, table_name;

SELECT code, rank, is_healthy, is_active
FROM plant.health_levels
WHERE code IN ('UNKNOWN', 'HEALTHY', 'MILD', 'MODERATE', 'SEVERE')
ORDER BY rank NULLS FIRST, code;

SELECT code, revision_number, condition_type, is_active
FROM plant.plant_conditions
WHERE code IN ('BROWN_SPOT', 'ANTHRACNOSE', 'SUNBURN', 'MECHANICAL_SCAR')
ORDER BY code, revision_number;

SELECT assessment_granularity, criteria_version, count(*) AS assessment_count
FROM survey.harvest_readiness_assessments
GROUP BY assessment_granularity, criteria_version
ORDER BY assessment_granularity, criteria_version;

SELECT count(*) AS ai_jobs_without_model_but_with_threshold
FROM mission.ai_processing_jobs
WHERE threshold_profile_id IS NOT NULL
  AND model_version_id IS NULL;

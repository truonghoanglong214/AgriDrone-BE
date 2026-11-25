\set ON_ERROR_STOP on

BEGIN;

-- Serialize this setup with model registration and activation changes.
LOCK TABLE mission.ai_model_versions IN SHARE ROW EXCLUSIVE MODE;

WITH requested AS (
    SELECT
        :'model_id'::uuid AS model_id,
        btrim(:'policy_version') AS policy_version,
        :'confidence'::numeric AS confidence,
        :'cluster_radius_meters'::numeric AS radius,
        :'buffer_meters'::numeric AS buffer
), policy AS (
    SELECT model_id, jsonb_build_object(
        'version', policy_version,
        'confidence', confidence,
        'clusterRadiusMeters', radius,
        'bufferMeters', buffer
    ) AS config
    FROM requested
    WHERE length(policy_version) BETWEEN 1 AND 128
      AND confidence BETWEEN 0 AND 1
      AND radius > 0 AND radius < 'Infinity'::numeric
      AND buffer > 0 AND buffer < 'Infinity'::numeric
), updated AS (
    UPDATE mission.ai_model_versions AS model
    SET metrics = jsonb_set(model.metrics, '{comboInferencePolicy}', policy.config),
        updated_at = CASE
            WHEN model.metrics -> 'comboInferencePolicy' = policy.config THEN model.updated_at
            ELSE now()
        END
    FROM policy
    WHERE model.id = policy.model_id
      AND model.is_active
      AND model.model_type = 'DISEASE_DETECTION'
      AND jsonb_typeof(model.metrics) = 'object'
      AND NOT EXISTS (
          SELECT 1
          FROM mission.ai_model_versions AS other
          WHERE other.id <> model.id
            AND other.is_active
            AND other.model_type = 'DISEASE_DETECTION'
      )
      AND (
          NOT (model.metrics ? 'comboInferencePolicy')
          OR model.metrics -> 'comboInferencePolicy' = policy.config
      )
    RETURNING model.id, model.model_name, model.version, model.metrics -> 'comboInferencePolicy' AS config
)
SELECT id, model_name, version, config FROM updated
\gset

\echo Configured model :model_name version :version with :config
COMMIT;

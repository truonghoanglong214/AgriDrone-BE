\set ON_ERROR_STOP on

BEGIN;

LOCK TABLE mission.ai_model_versions IN SHARE ROW EXCLUSIVE MODE;

-- Registers and activates the default FastAPI disease detection model with provenance
-- metadata and comboInferencePolicy when no active DISEASE_DETECTION model exists yet.
INSERT INTO mission.ai_model_versions (
    id,
    model_name,
    version,
    model_type,
    metrics,
    is_active,
    created_at,
    updated_at
)
SELECT
    '00000000-0000-0000-0000-000000000002'::uuid,
    'best_disease.pt (Disease-Detection)',
    '1.0.0',
    'DISEASE_DETECTION'::system.ai_model_type,
    '{
      "datasetId": "dragonfruit_health_v1",
      "datasetVersion": "1.0.0",
      "annotationRuleVersion": "AGRIDRONE_ANNOTATION_RULES_V1",
      "boundaryPolicyVersion": "STRICT_BOUNDARY_V1",
      "teamLabelLimitation": "Team-labelled pilot observations; preliminary disease screening only.",
      "comboInferencePolicy": {
        "version": "combo-v1",
        "confidence": 0.25,
        "clusterRadiusMeters": 15.0,
        "bufferMeters": 3.0
      }
    }'::jsonb,
    true,
    '2026-01-01T00:00:00Z'::timestamptz,
    '2026-01-01T00:00:00Z'::timestamptz
WHERE NOT EXISTS (
    SELECT 1
    FROM mission.ai_model_versions
    WHERE model_type = 'DISEASE_DETECTION'::system.ai_model_type
      AND is_active = true
)
ON CONFLICT (model_name, version) DO NOTHING;

COMMIT;

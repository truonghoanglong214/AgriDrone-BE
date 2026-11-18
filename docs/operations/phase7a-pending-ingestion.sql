START TRANSACTION;
ALTER TABLE plant.disease_zones DROP CONSTRAINT ck_disease_zones_lifecycle;

ALTER TABLE survey.survey_results ADD active_import_batch_id uuid;

ALTER TABLE survey.survey_results ADD farm_base_map_version_id uuid;

ALTER TABLE survey.survey_results ADD farm_boundary_version_id uuid;

ALTER TABLE survey.survey_results ADD observed_at timestamp with time zone;

ALTER TABLE survey.survey_results ADD result_nature character varying(40);

ALTER TABLE survey.survey_results ADD source_mission_id uuid;

ALTER TABLE survey.survey_results ADD team_label_limitation character varying(2000);

ALTER TABLE plant.disease_zones ALTER COLUMN health_level_id DROP NOT NULL;

ALTER TABLE plant.disease_zones ADD guidance_suggestions jsonb;

ALTER TABLE plant.disease_zones ADD has_blocking_items boolean NOT NULL DEFAULT FALSE;

ALTER TABLE plant.disease_zones ADD import_batch_id uuid;

ALTER TABLE plant.disease_zones ADD selected_sourced_guidance_id uuid;

ALTER TABLE plant.disease_zones ADD source_schema_version integer NOT NULL DEFAULT 3;

ALTER TABLE plant.disease_zones ADD suspected_class_code character varying(80);

ALTER TABLE plant.disease_zone_memberships ALTER COLUMN plant_id DROP NOT NULL;

ALTER TABLE plant.disease_zone_memberships ADD mapping_candidate_id uuid;

ALTER TABLE survey.survey_results ADD CONSTRAINT uq_survey_results_id_tenant_farm UNIQUE (id, tenant_id, farm_id);

CREATE TABLE plant.plant_health_findings (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    mission_id uuid NOT NULL,
    source_finding_id uuid NOT NULL,
    source_version integer NOT NULL,
    plant_id uuid,
    mapping_candidate_id uuid,
    supersedes_finding_id uuid,
    original_position geometry(Point,4326) NOT NULL,
    suspected_class_code character varying(80),
    resolution_state character varying(40) NOT NULL,
    boundary_classification character varying(40) NOT NULL,
    fingerprint character varying(64) NOT NULL,
    original jsonb NOT NULL,
    observed_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_plant_health_findings PRIMARY KEY (id),
    CONSTRAINT uq_health_finding_context UNIQUE (id, tenant_id, farm_id, survey_result_id),
    CONSTRAINT ck_health_finding_point CHECK (ST_IsValid(original_position) AND NOT ST_IsEmpty(original_position) AND ST_SRID(original_position) = 4326),
    CONSTRAINT ck_health_finding_resolution CHECK (resolution_state IN ('PENDING_REVIEW', 'NEEDS_REVIEW')),
    CONSTRAINT ck_health_finding_version CHECK (source_version > 0 AND length(fingerprint) = 64),
    CONSTRAINT fk_health_finding_plant FOREIGN KEY (plant_id, farm_id) REFERENCES plant.plants (id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_finding_result FOREIGN KEY (survey_result_id, tenant_id, farm_id) REFERENCES survey.survey_results (id, tenant_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_finding_supersedes FOREIGN KEY (supersedes_finding_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.plant_health_findings (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

CREATE TABLE survey.plant_health_import_batches (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_order_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    mission_id uuid NOT NULL,
    handoff_id uuid NOT NULL,
    job_id uuid NOT NULL,
    farm_boundary_version_id uuid NOT NULL,
    farm_base_map_version_id uuid NOT NULL,
    source_version integer NOT NULL,
    supersedes_batch_id uuid,
    fingerprint character varying(64) NOT NULL,
    original_payload jsonb NOT NULL,
    produced_at timestamp with time zone NOT NULL,
    imported_at timestamp with time zone NOT NULL,
    blocking_count integer NOT NULL,
    CONSTRAINT pk_plant_health_import_batches PRIMARY KEY (id),
    CONSTRAINT uq_health_import_context UNIQUE (id, tenant_id, farm_id, survey_result_id),
    CONSTRAINT ck_health_import_fingerprint CHECK (length(fingerprint) = 64),
    CONSTRAINT ck_health_import_source_version CHECK (source_version > 0 AND blocking_count >= 0),
    CONSTRAINT fk_health_import_result_context FOREIGN KEY (survey_result_id, survey_order_id, farm_id) REFERENCES survey.survey_results (id, survey_order_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_import_result_tenant FOREIGN KEY (survey_result_id, tenant_id, farm_id) REFERENCES survey.survey_results (id, tenant_id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_import_supersedes FOREIGN KEY (supersedes_batch_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

CREATE TABLE plant.plant_health_batch_findings (
    batch_id uuid NOT NULL,
    finding_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    CONSTRAINT pk_plant_health_batch_findings PRIMARY KEY (batch_id, finding_id),
    CONSTRAINT fk_health_batch_finding_batch FOREIGN KEY (batch_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_batch_finding_context FOREIGN KEY (finding_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.plant_health_findings (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

CREATE TABLE plant.plant_health_boundary_exceptions (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    source_reference_id character varying(200) NOT NULL,
    original_position geometry(Point,4326) NOT NULL,
    original jsonb NOT NULL,
    status character varying(40) NOT NULL,
    CONSTRAINT pk_plant_health_boundary_exceptions PRIMARY KEY (id),
    CONSTRAINT fk_health_boundary_exception_batch FOREIGN KEY (batch_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

CREATE INDEX "IX_disease_zones_import_batch_id_tenant_id_farm_id_survey_resu~" ON plant.disease_zones (import_batch_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_disease_zones_selected_sourced_guidance_id" ON plant.disease_zones (selected_sourced_guidance_id);

ALTER TABLE plant.disease_zones ADD CONSTRAINT ck_disease_zones_lifecycle CHECK ((status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL));

ALTER TABLE plant.disease_zones ADD CONSTRAINT ck_disease_zones_source_schema CHECK ((source_schema_version = 3 AND health_level_id IS NOT NULL AND import_batch_id IS NULL) OR (source_schema_version = 4 AND health_level_id IS NULL AND import_batch_id IS NOT NULL AND suspected_class_code IS NOT NULL AND selected_treatment_recommendation_id IS NULL AND guidance_suggestions IS NOT NULL));

CREATE UNIQUE INDEX uq_disease_zone_memberships_candidate ON plant.disease_zone_memberships (disease_zone_id, membership_version, mapping_candidate_id);

ALTER TABLE plant.disease_zone_memberships ADD CONSTRAINT ck_disease_zone_memberships_reference CHECK (plant_id IS NOT NULL OR mapping_candidate_id IS NOT NULL);

CREATE INDEX "IX_plant_health_batch_findings_batch_id_tenant_id_farm_id_surv~" ON plant.plant_health_batch_findings (batch_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_batch_findings_finding_id_tenant_id_farm_id_su~" ON plant.plant_health_batch_findings (finding_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_boundary_exceptions_batch_id_tenant_id_farm_id~" ON plant.plant_health_boundary_exceptions (batch_id, tenant_id, farm_id, survey_result_id);

CREATE UNIQUE INDEX uq_health_boundary_exception_source ON plant.plant_health_boundary_exceptions (batch_id, source_reference_id);

CREATE INDEX ix_health_finding_review_queue ON plant.plant_health_findings (survey_result_id, resolution_state);

CREATE INDEX "IX_plant_health_findings_plant_id_farm_id" ON plant.plant_health_findings (plant_id, farm_id);

CREATE INDEX "IX_plant_health_findings_supersedes_finding_id_tenant_id_farm_~" ON plant.plant_health_findings (supersedes_finding_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_findings_survey_result_id_tenant_id_farm_id" ON plant.plant_health_findings (survey_result_id, tenant_id, farm_id);

CREATE UNIQUE INDEX uq_health_finding_source ON plant.plant_health_findings (tenant_id, mission_id, source_finding_id, source_version);

CREATE INDEX ix_health_import_result_version ON survey.plant_health_import_batches (survey_result_id, source_version);

CREATE INDEX "IX_plant_health_import_batches_supersedes_batch_id_tenant_id_f~" ON survey.plant_health_import_batches (supersedes_batch_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_import_batches_survey_result_id_survey_order_i~" ON survey.plant_health_import_batches (survey_result_id, survey_order_id, farm_id);

CREATE INDEX "IX_plant_health_import_batches_survey_result_id_tenant_id_farm~" ON survey.plant_health_import_batches (survey_result_id, tenant_id, farm_id);

CREATE UNIQUE INDEX uq_health_import_handoff ON survey.plant_health_import_batches (tenant_id, handoff_id);

CREATE UNIQUE INDEX uq_health_import_job ON survey.plant_health_import_batches (tenant_id, mission_id, job_id);

ALTER TABLE plant.disease_zones ADD CONSTRAINT fk_disease_zones_sourced_guidance FOREIGN KEY (selected_sourced_guidance_id) REFERENCES plant.sourced_guidance (id) ON DELETE RESTRICT;

ALTER TABLE plant.disease_zones ADD CONSTRAINT fk_health_zone_batch FOREIGN KEY (import_batch_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT;

INSERT INTO system.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261109040913_Step7APlantHealthPendingIngestion', '10.0.10');

COMMIT;

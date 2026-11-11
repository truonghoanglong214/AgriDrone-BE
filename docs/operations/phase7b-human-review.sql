START TRANSACTION;
ALTER TABLE plant.disease_zones DROP CONSTRAINT ck_disease_zones_lifecycle;

ALTER TABLE survey.survey_results ADD review_revision bigint NOT NULL DEFAULT 0;

ALTER TABLE survey.survey_results ADD review_updated_at timestamp with time zone;

ALTER TABLE plant.disease_zones ADD current_health_review_id uuid;

ALTER TABLE plant.disease_zones ADD guidance_decision character varying(40);

ALTER TABLE plant.disease_zones ADD reviewed_plant_condition_id uuid;

ALTER TABLE plant.disease_zones ADD reviewed_suspected_class_code character varying(80);

ALTER TABLE survey.plant_health_import_batches ADD CONSTRAINT uq_health_import_result_tenant UNIQUE (id, tenant_id, survey_result_id);

ALTER TABLE plant.plant_health_boundary_exceptions ADD CONSTRAINT uq_health_boundary_review_context UNIQUE (id, batch_id, tenant_id, farm_id, survey_result_id);

ALTER TABLE plant.disease_zones ADD CONSTRAINT uq_disease_zones_review_context UNIQUE (id, tenant_id, farm_id, survey_result_id);

CREATE TABLE survey.plant_health_review_operations (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    operation_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    fingerprint character varying(64) NOT NULL,
    outcome jsonb NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_plant_health_review_operations PRIMARY KEY (id),
    CONSTRAINT fk_health_review_operation_batch FOREIGN KEY (batch_id, tenant_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, survey_result_id) ON DELETE RESTRICT
);

CREATE TABLE plant.plant_health_reviews (
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    kind character varying(32) NOT NULL,
    subject_id uuid NOT NULL,
    finding_id uuid,
    zone_id uuid,
    boundary_exception_id uuid,
    previous_review_id uuid,
    decision character varying(40) NOT NULL,
    reviewed_by uuid NOT NULL,
    reviewed_at timestamp with time zone NOT NULL,
    reason character varying(2000) NOT NULL,
    snapshot jsonb NOT NULL,
    CONSTRAINT pk_plant_health_reviews PRIMARY KEY (id),
    CONSTRAINT uq_health_review_context UNIQUE (id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id),
    CONSTRAINT ck_health_review_decision CHECK ((kind = 'Finding' AND decision IN ('Approve','Correct','Reject','NonDisease','Ambiguous')) OR (kind = 'Zone' AND decision IN ('Approve','Correct','Reject','Ambiguous')) OR (kind = 'Boundary' AND decision IN ('AcceptCorrectedPosition','ExcludeEvidence','NeedsEvidence')) OR (kind = 'Guidance' AND decision IN ('Select','RejectAll','NeedsEvidence'))),
    CONSTRAINT ck_health_review_subject CHECK ((kind = 'Finding' AND finding_id = subject_id AND finding_id IS NOT NULL AND zone_id IS NULL AND boundary_exception_id IS NULL) OR (kind IN ('Zone','Guidance') AND zone_id = subject_id AND zone_id IS NOT NULL AND finding_id IS NULL AND boundary_exception_id IS NULL) OR (kind = 'Boundary' AND boundary_exception_id = subject_id AND boundary_exception_id IS NOT NULL AND finding_id IS NULL AND zone_id IS NULL)),
    CONSTRAINT fk_health_review_batch_finding FOREIGN KEY (batch_id, finding_id) REFERENCES plant.plant_health_batch_findings (batch_id, finding_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_review_boundary_context FOREIGN KEY (boundary_exception_id, batch_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.plant_health_boundary_exceptions (id, batch_id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_review_finding_context FOREIGN KEY (finding_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.plant_health_findings (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_review_import_context FOREIGN KEY (batch_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_review_previous FOREIGN KEY (previous_review_id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.plant_health_reviews (id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_review_zone_context FOREIGN KEY (zone_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.disease_zones (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

CREATE TABLE plant.plant_health_review_states (
    batch_id uuid NOT NULL,
    kind character varying(32) NOT NULL,
    subject_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    current_review_id uuid NOT NULL,
    CONSTRAINT pk_plant_health_review_states PRIMARY KEY (batch_id, kind, subject_id),
    CONSTRAINT fk_health_review_state_current FOREIGN KEY (current_review_id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id) REFERENCES plant.plant_health_reviews (id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

ALTER TABLE plant.disease_zones ADD CONSTRAINT ck_disease_zones_lifecycle CHECK ((source_schema_version = 4 AND ((status = 'PROPOSED' AND published_at IS NULL AND published_by IS NULL) OR (status = 'REJECTED' AND current_health_review_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND reviewed_geometry IS NULL AND published_at IS NULL AND published_by IS NULL) OR (status = 'REVIEWED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND has_blocking_items = FALSE AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NULL AND published_by IS NULL))) OR (source_schema_version = 3 AND ((status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL))));

CREATE INDEX "IX_plant_health_review_operations_batch_id_tenant_id_survey_re~" ON survey.plant_health_review_operations (batch_id, tenant_id, survey_result_id);

CREATE UNIQUE INDEX uq_health_review_operation ON survey.plant_health_review_operations (tenant_id, actor_id, operation_id);

CREATE INDEX "IX_plant_health_review_states_current_review_id_batch_id_kind_~" ON plant.plant_health_review_states (current_review_id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX ix_health_review_history ON plant.plant_health_reviews (batch_id, kind, subject_id, reviewed_at, id);

CREATE INDEX "IX_plant_health_reviews_batch_id_finding_id" ON plant.plant_health_reviews (batch_id, finding_id);

CREATE INDEX "IX_plant_health_reviews_batch_id_tenant_id_farm_id_survey_resu~" ON plant.plant_health_reviews (batch_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_reviews_boundary_exception_id_batch_id_tenant_~" ON plant.plant_health_reviews (boundary_exception_id, batch_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_reviews_finding_id_tenant_id_farm_id_survey_re~" ON plant.plant_health_reviews (finding_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_reviews_previous_review_id_batch_id_kind_subje~" ON plant.plant_health_reviews (previous_review_id, batch_id, kind, subject_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_reviews_zone_id_tenant_id_farm_id_survey_resul~" ON plant.plant_health_reviews (zone_id, tenant_id, farm_id, survey_result_id);

CREATE FUNCTION plant.guard_health_review_history() RETURNS trigger
LANGUAGE plpgsql AS $guard$
BEGIN
    RAISE EXCEPTION 'Plant Health review history and operation receipts are append-only.';
END $guard$;
CREATE TRIGGER trg_health_reviews_append_only
    BEFORE UPDATE OR DELETE ON plant.plant_health_reviews
    FOR EACH ROW EXECUTE FUNCTION plant.guard_health_review_history();
CREATE TRIGGER trg_health_review_operations_append_only
    BEFORE UPDATE OR DELETE ON survey.plant_health_review_operations
    FOR EACH ROW EXECUTE FUNCTION plant.guard_health_review_history();
CREATE FUNCTION plant.guard_health_review_zone_batch() RETURNS trigger
LANGUAGE plpgsql AS $guard$
BEGIN
    IF NEW.zone_id IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM plant.disease_zones z
        WHERE z.id = NEW.zone_id AND z.import_batch_id = NEW.batch_id
          AND z.tenant_id = NEW.tenant_id AND z.farm_id = NEW.farm_id
          AND z.survey_result_id = NEW.survey_result_id AND z.source_schema_version = 4
    ) THEN
        RAISE EXCEPTION 'Zone review must belong to the source import batch.' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $guard$;
CREATE TRIGGER trg_health_review_zone_batch BEFORE INSERT ON plant.plant_health_reviews
    FOR EACH ROW EXECUTE FUNCTION plant.guard_health_review_zone_batch();

INSERT INTO system.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261109052104_Step7BPlantHealthHumanReview', '10.0.10');

COMMIT;

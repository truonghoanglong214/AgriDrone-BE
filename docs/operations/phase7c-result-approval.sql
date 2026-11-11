START TRANSACTION;
ALTER TABLE survey.survey_results ADD approval_fingerprint character varying(64);

ALTER TABLE survey.survey_results ADD approval_snapshot jsonb;

ALTER TABLE survey.survey_results ADD approved_batch_id uuid;

ALTER TABLE survey.survey_results ADD approved_review_revision bigint;

CREATE INDEX "IX_survey_results_approved_batch_id_tenant_id_farm_id_id" ON survey.survey_results (approved_batch_id, tenant_id, farm_id, id);

ALTER TABLE survey.survey_results ADD CONSTRAINT ck_survey_results_approval_snapshot CHECK ((result_nature IS NULL AND approved_batch_id IS NULL AND approved_review_revision IS NULL AND approval_snapshot IS NULL AND approval_fingerprint IS NULL) OR (result_nature IS NOT NULL AND result_nature = 'PRELIMINARY_SUSPECTED' AND ((status = 'PENDING_REVIEW' AND approved_batch_id IS NULL AND approved_review_revision IS NULL AND approval_snapshot IS NULL AND approval_fingerprint IS NULL) OR (status IN ('APPROVED','PUBLISHED') AND service_type = 'PLANT_HEALTH' AND approved_batch_id IS NOT NULL AND active_import_batch_id IS NOT NULL AND approved_batch_id = active_import_batch_id AND approved_review_revision IS NOT NULL AND approved_review_revision = review_revision AND source_mission_id IS NOT NULL AND farm_boundary_version_id IS NOT NULL AND farm_base_map_version_id IS NOT NULL AND approval_snapshot IS NOT NULL AND jsonb_typeof(approval_snapshot) = 'object' AND approval_fingerprint IS NOT NULL AND length(approval_fingerprint) = 64 AND team_label_limitation IS NOT NULL AND length(btrim(team_label_limitation)) > 0))));

ALTER TABLE survey.survey_results ADD CONSTRAINT fk_survey_results_approved_batch_context FOREIGN KEY (approved_batch_id, tenant_id, farm_id, id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT;

CREATE FUNCTION survey.guard_plant_health_approval() RETURNS trigger
LANGUAGE plpgsql AS $guard$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.approval_snapshot IS NOT NULL THEN
            RAISE EXCEPTION 'Approved Plant Health snapshots cannot be deleted.';
        END IF;
        RETURN OLD;
    END IF;
    IF OLD.approval_snapshot IS NOT NULL AND (
        NEW.approval_snapshot IS DISTINCT FROM OLD.approval_snapshot OR
        NEW.approval_fingerprint IS DISTINCT FROM OLD.approval_fingerprint OR
        NEW.approved_batch_id IS DISTINCT FROM OLD.approved_batch_id OR
        NEW.approved_review_revision IS DISTINCT FROM OLD.approved_review_revision OR
        NEW.active_import_batch_id IS DISTINCT FROM OLD.active_import_batch_id OR
        NEW.review_revision IS DISTINCT FROM OLD.review_revision OR
        NEW.reviewed_by IS DISTINCT FROM OLD.reviewed_by OR NEW.reviewed_at IS DISTINCT FROM OLD.reviewed_at OR
        NEW.source_mission_id IS DISTINCT FROM OLD.source_mission_id OR
        NEW.farm_boundary_version_id IS DISTINCT FROM OLD.farm_boundary_version_id OR
        NEW.farm_base_map_version_id IS DISTINCT FROM OLD.farm_base_map_version_id OR
        NEW.result_nature IS DISTINCT FROM OLD.result_nature OR
        NEW.team_label_limitation IS DISTINCT FROM OLD.team_label_limitation OR
        NEW.observed_at IS DISTINCT FROM OLD.observed_at OR NEW.provenance IS DISTINCT FROM OLD.provenance OR
        NEW.status NOT IN ('APPROVED','PUBLISHED')
    ) THEN
        RAISE EXCEPTION 'The approved review candidate is immutable; publication must retain its approval evidence.';
    END IF;
    RETURN NEW;
END $guard$;
CREATE TRIGGER trg_plant_health_approval_immutable
    BEFORE UPDATE OR DELETE ON survey.survey_results
    FOR EACH ROW EXECUTE FUNCTION survey.guard_plant_health_approval();

INSERT INTO system.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261109054451_Step7CPlantHealthResultApproval', '10.0.10');

COMMIT;

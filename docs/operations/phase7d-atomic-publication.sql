START TRANSACTION;
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM survey.survey_results WHERE result_nature = 'PRELIMINARY_SUSPECTED' AND status = 'PUBLISHED') THEN
        RAISE EXCEPTION 'Existing V4 Published rows need explicit publication-history reconciliation before Step7D; no synthetic history is generated.';
    END IF;
END $guard$;

ALTER TABLE plant.disease_zones DROP CONSTRAINT ck_disease_zones_lifecycle;

ALTER TABLE plant.disease_zones ADD CONSTRAINT ck_disease_zones_lifecycle CHECK ((source_schema_version = 4 AND ((status = 'PROPOSED' AND published_at IS NULL AND published_by IS NULL) OR (status = 'REJECTED' AND current_health_review_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND reviewed_geometry IS NULL AND published_at IS NULL AND published_by IS NULL) OR (status = 'REVIEWED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND has_blocking_items = FALSE AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NULL AND published_by IS NULL) OR (status = 'PUBLISHED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND current_membership_version > 1 AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND has_blocking_items = FALSE AND guidance_decision IS NOT NULL AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NOT NULL AND published_by IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL))) OR (source_schema_version = 3 AND ((status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL))));

CREATE TABLE survey.plant_health_publications (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    publication_version bigint NOT NULL,
    published_by uuid NOT NULL,
    published_at timestamp with time zone NOT NULL,
    approval_fingerprint character varying(64) NOT NULL,
    fingerprint character varying(64) NOT NULL,
    payload jsonb NOT NULL,
    CONSTRAINT pk_plant_health_publications PRIMARY KEY (id),
    CONSTRAINT uq_health_publication_context UNIQUE (id, tenant_id, farm_id, survey_result_id),
    CONSTRAINT ck_health_publication_payload CHECK (publication_version > 0 AND length(approval_fingerprint) = 64 AND length(fingerprint) = 64 AND jsonb_typeof(payload) = 'object'),
    CONSTRAINT fk_health_publication_batch FOREIGN KEY (batch_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_import_batches (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT,
    CONSTRAINT fk_health_publication_result FOREIGN KEY (survey_result_id, tenant_id, farm_id) REFERENCES survey.survey_results (id, tenant_id, farm_id) ON DELETE RESTRICT
);

CREATE TABLE plant.latest_reviewed_plant_health (
    plant_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    farm_id uuid NOT NULL,
    publication_id uuid NOT NULL,
    survey_result_id uuid NOT NULL,
    observed_at timestamp with time zone NOT NULL,
    publication_version bigint NOT NULL,
    result_nature character varying(40) NOT NULL,
    team_label_limitation character varying(2000) NOT NULL,
    findings jsonb NOT NULL,
    CONSTRAINT pk_latest_reviewed_plant_health PRIMARY KEY (plant_id),
    CONSTRAINT ck_latest_reviewed_health_preliminary CHECK (publication_version > 0 AND result_nature = 'PRELIMINARY_SUSPECTED' AND length(btrim(team_label_limitation)) > 0 AND jsonb_typeof(findings) = 'array' AND jsonb_array_length(findings) > 0),
    CONSTRAINT fk_latest_reviewed_health_plant FOREIGN KEY (plant_id, farm_id) REFERENCES plant.plants (id, farm_id) ON DELETE RESTRICT,
    CONSTRAINT fk_latest_reviewed_health_publication FOREIGN KEY (publication_id, tenant_id, farm_id, survey_result_id) REFERENCES survey.plant_health_publications (id, tenant_id, farm_id, survey_result_id) ON DELETE RESTRICT
);

CREATE INDEX ix_latest_reviewed_health_farm ON plant.latest_reviewed_plant_health (tenant_id, farm_id);

CREATE INDEX "IX_latest_reviewed_plant_health_plant_id_farm_id" ON plant.latest_reviewed_plant_health (plant_id, farm_id);

CREATE INDEX "IX_latest_reviewed_plant_health_publication_id_tenant_id_farm_~" ON plant.latest_reviewed_plant_health (publication_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_publications_batch_id_tenant_id_farm_id_survey~" ON survey.plant_health_publications (batch_id, tenant_id, farm_id, survey_result_id);

CREATE INDEX "IX_plant_health_publications_survey_result_id_tenant_id_farm_id" ON survey.plant_health_publications (survey_result_id, tenant_id, farm_id);

CREATE UNIQUE INDEX uq_health_publication_farm_version ON survey.plant_health_publications (tenant_id, farm_id, publication_version);

CREATE UNIQUE INDEX uq_health_publication_result ON survey.plant_health_publications (survey_result_id);

CREATE FUNCTION survey.guard_health_publication_history() RETURNS trigger
LANGUAGE plpgsql AS $guard$
BEGIN
    RAISE EXCEPTION 'Plant Health publication history is immutable.' USING ERRCODE = '23514';
END $guard$;
CREATE TRIGGER trg_health_publication_immutable BEFORE UPDATE OR DELETE ON survey.plant_health_publications
    FOR EACH ROW EXECUTE FUNCTION survey.guard_health_publication_history();

CREATE FUNCTION survey.guard_published_health_result() RETURNS trigger
LANGUAGE plpgsql AS $guard$
BEGIN
    IF OLD.result_nature = 'PRELIMINARY_SUSPECTED' AND OLD.status = 'PUBLISHED' THEN
        RAISE EXCEPTION 'Published Plant Health result metadata is immutable.' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $guard$;
CREATE TRIGGER trg_published_health_result_immutable BEFORE UPDATE OR DELETE ON survey.survey_results
    FOR EACH ROW EXECUTE FUNCTION survey.guard_published_health_result();

CREATE FUNCTION survey.check_health_publication_commit() RETURNS trigger
LANGUAGE plpgsql AS $guard$
DECLARE result_id uuid;
BEGIN
    IF TG_TABLE_NAME = 'survey_results' THEN
        IF NEW.result_nature IS DISTINCT FROM 'PRELIMINARY_SUSPECTED' OR NEW.status <> 'PUBLISHED' THEN RETURN NEW; END IF;
        result_id := NEW.id;
    ELSE
        result_id := NEW.survey_result_id;
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM survey.plant_health_publications p
        JOIN survey.survey_results r ON r.id = p.survey_result_id AND r.tenant_id = p.tenant_id AND r.farm_id = p.farm_id
        JOIN survey.survey_orders o ON o.id = r.survey_order_id AND o.tenant_id = r.tenant_id AND o.farm_id = r.farm_id
        WHERE r.id = result_id AND r.status = 'PUBLISHED' AND o.status = 'COMPLETED'
          AND r.result_nature = 'PRELIMINARY_SUSPECTED' AND p.batch_id = r.approved_batch_id
          AND p.approval_fingerprint = r.approval_fingerprint
          AND p.published_by = r.published_by AND p.published_at = r.published_at
    ) THEN
        RAISE EXCEPTION 'Publication, frozen approval and completed Order must commit together.' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $guard$;
CREATE CONSTRAINT TRIGGER trg_health_publication_commit AFTER INSERT ON survey.plant_health_publications
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION survey.check_health_publication_commit();
CREATE CONSTRAINT TRIGGER trg_health_result_publication_commit AFTER INSERT OR UPDATE ON survey.survey_results
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION survey.check_health_publication_commit();

CREATE FUNCTION plant.guard_latest_reviewed_health() RETURNS trigger
LANGUAGE plpgsql AS $guard$
DECLARE source_payload jsonb; source_version bigint; expected_findings jsonb; expected_observed timestamptz;
BEGIN
    IF TG_OP = 'UPDATE' AND (
        NEW.plant_id IS DISTINCT FROM OLD.plant_id OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id OR NEW.farm_id IS DISTINCT FROM OLD.farm_id OR
        (NEW.observed_at, NEW.publication_version, NEW.survey_result_id) <= (OLD.observed_at, OLD.publication_version, OLD.survey_result_id)
    ) THEN
        RAISE EXCEPTION 'Latest-reviewed context is fixed and its ordering key must advance.' USING ERRCODE = '23514';
    END IF;
    SELECT p.payload, p.publication_version INTO source_payload, source_version FROM survey.plant_health_publications p
    WHERE p.id = NEW.publication_id AND p.tenant_id = NEW.tenant_id AND p.farm_id = NEW.farm_id AND p.survey_result_id = NEW.survey_result_id;
    SELECT jsonb_agg(item.value ORDER BY item.position), max((item.value->>'ObservedAt')::timestamptz)
        INTO expected_findings, expected_observed
        FROM jsonb_array_elements(source_payload->'Findings') WITH ORDINALITY item(value, position)
        WHERE item.value->>'PlantId' = NEW.plant_id::text;
    IF expected_findings IS NULL OR NEW.findings IS DISTINCT FROM expected_findings OR NEW.observed_at IS DISTINCT FROM expected_observed OR
        NEW.publication_version IS DISTINCT FROM source_version OR NEW.result_nature IS DISTINCT FROM source_payload->>'ResultNature' OR
        NEW.team_label_limitation IS DISTINCT FROM source_payload->>'TeamLabelLimitation'
    THEN
        RAISE EXCEPTION 'Latest-reviewed values must match the complete published Plant finding set.' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $guard$;
CREATE TRIGGER trg_latest_reviewed_health_guard BEFORE INSERT OR UPDATE ON plant.latest_reviewed_plant_health
    FOR EACH ROW EXECUTE FUNCTION plant.guard_latest_reviewed_health();

CREATE FUNCTION plant.guard_published_health_zone() RETURNS trigger
LANGUAGE plpgsql AS $guard$
BEGIN
    IF OLD.source_schema_version = 4 AND OLD.status IN ('PUBLISHED','SUPERSEDED') THEN
        RAISE EXCEPTION 'Published Plant Health zone snapshots are immutable.' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $guard$;
CREATE TRIGGER trg_published_health_zone_immutable BEFORE UPDATE OR DELETE ON plant.disease_zones
    FOR EACH ROW EXECUTE FUNCTION plant.guard_published_health_zone();
CREATE FUNCTION plant.guard_published_health_membership() RETURNS trigger
LANGUAGE plpgsql AS $guard$
DECLARE old_zone uuid; new_zone uuid;
BEGIN
    IF TG_OP <> 'INSERT' THEN old_zone := OLD.disease_zone_id; END IF;
    IF TG_OP <> 'DELETE' THEN new_zone := NEW.disease_zone_id; END IF;
    IF EXISTS (SELECT 1 FROM plant.disease_zones z WHERE z.id IN (old_zone, new_zone)
        AND z.source_schema_version = 4 AND z.status IN ('PUBLISHED','SUPERSEDED')) THEN
        RAISE EXCEPTION 'Published Plant Health memberships are immutable.' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $guard$;
CREATE TRIGGER trg_published_health_membership_immutable BEFORE INSERT OR UPDATE OR DELETE ON plant.disease_zone_memberships
    FOR EACH ROW EXECUTE FUNCTION plant.guard_published_health_membership();

INSERT INTO system.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20261109061911_Step7DPlantHealthAtomicPublication', '10.0.10');

COMMIT;

using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step7DPlantHealthAtomicPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.survey_results WHERE result_nature = 'PRELIMINARY_SUSPECTED' AND status = 'PUBLISHED') THEN
                        RAISE EXCEPTION 'Existing V4 Published rows need explicit publication-history reconciliation before Step7D; no synthetic history is generated.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_disease_zones_lifecycle", schema: "plant", table: "disease_zones");
            migrationBuilder.AddCheckConstraint(name: "ck_disease_zones_lifecycle", schema: "plant", table: "disease_zones", sql: "(source_schema_version = 4 AND ((status = 'PROPOSED' AND published_at IS NULL AND published_by IS NULL) OR (status = 'REJECTED' AND current_health_review_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND reviewed_geometry IS NULL AND published_at IS NULL AND published_by IS NULL) OR (status = 'REVIEWED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND has_blocking_items = FALSE AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NULL AND published_by IS NULL) OR (status = 'PUBLISHED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND current_membership_version > 1 AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND has_blocking_items = FALSE AND guidance_decision IS NOT NULL AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NOT NULL AND published_by IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL))) OR (source_schema_version = 3 AND ((status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)))");
            migrationBuilder.CreateTable(
                name: "plant_health_publications",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_version = table.Column<long>(type: "bigint", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approval_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_publications", x => x.id);
                    table.UniqueConstraint("uq_health_publication_context", x => new { x.id, x.tenant_id, x.farm_id, x.survey_result_id });
                    table.CheckConstraint("ck_health_publication_payload", "publication_version > 0 AND length(approval_fingerprint) = 64 AND length(fingerprint) = 64 AND jsonb_typeof(payload) = 'object'");
                    table.ForeignKey(
                        name: "fk_health_publication_batch",
                        columns: x => new { x.batch_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_import_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_publication_result",
                        columns: x => new { x.survey_result_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "latest_reviewed_plant_health",
                schema: "plant",
                columns: table => new
                {
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publication_version = table.Column<long>(type: "bigint", nullable: false),
                    result_nature = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    team_label_limitation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    findings = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_latest_reviewed_plant_health", x => x.plant_id);
                    table.CheckConstraint("ck_latest_reviewed_health_preliminary", "publication_version > 0 AND result_nature = 'PRELIMINARY_SUSPECTED' AND length(btrim(team_label_limitation)) > 0 AND jsonb_typeof(findings) = 'array' AND jsonb_array_length(findings) > 0");
                    table.ForeignKey(
                        name: "fk_latest_reviewed_health_plant",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_latest_reviewed_health_publication",
                        columns: x => new { x.publication_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_publications",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_latest_reviewed_health_farm",
                schema: "plant",
                table: "latest_reviewed_plant_health",
                columns: new[] { "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_latest_reviewed_plant_health_plant_id_farm_id",
                schema: "plant",
                table: "latest_reviewed_plant_health",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_latest_reviewed_plant_health_publication_id_tenant_id_farm_~",
                schema: "plant",
                table: "latest_reviewed_plant_health",
                columns: new[] { "publication_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_publications_batch_id_tenant_id_farm_id_survey~",
                schema: "survey",
                table: "plant_health_publications",
                columns: new[] { "batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_publications_survey_result_id_tenant_id_farm_id",
                schema: "survey",
                table: "plant_health_publications",
                columns: new[] { "survey_result_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_health_publication_farm_version",
                schema: "survey",
                table: "plant_health_publications",
                columns: new[] { "tenant_id", "farm_id", "publication_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_health_publication_result",
                schema: "survey",
                table: "plant_health_publications",
                column: "survey_result_id",
                unique: true);
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.plant_health_publications) OR EXISTS (SELECT 1 FROM plant.latest_reviewed_plant_health)
                        OR EXISTS (SELECT 1 FROM survey.survey_results WHERE result_nature = 'PRELIMINARY_SUSPECTED' AND status = 'PUBLISHED')
                        OR EXISTS (SELECT 1 FROM plant.disease_zones WHERE source_schema_version = 4 AND status IN ('PUBLISHED','SUPERSEDED')) THEN
                        RAISE EXCEPTION 'Step7D contains published evidence. Use roll-forward recovery or reconcile/archive explicitly.';
                    END IF;
                END $guard$;
                DROP TRIGGER trg_published_health_membership_immutable ON plant.disease_zone_memberships;
                DROP FUNCTION plant.guard_published_health_membership();
                DROP TRIGGER trg_published_health_zone_immutable ON plant.disease_zones;
                DROP FUNCTION plant.guard_published_health_zone();
                DROP TRIGGER trg_latest_reviewed_health_guard ON plant.latest_reviewed_plant_health;
                DROP FUNCTION plant.guard_latest_reviewed_health();
                DROP TRIGGER trg_health_publication_commit ON survey.plant_health_publications;
                DROP TRIGGER trg_health_result_publication_commit ON survey.survey_results;
                DROP FUNCTION survey.check_health_publication_commit();
                DROP TRIGGER trg_published_health_result_immutable ON survey.survey_results;
                DROP FUNCTION survey.guard_published_health_result();
                DROP TRIGGER trg_health_publication_immutable ON survey.plant_health_publications;
                DROP FUNCTION survey.guard_health_publication_history();
                """);
            migrationBuilder.DropCheckConstraint(name: "ck_disease_zones_lifecycle", schema: "plant", table: "disease_zones");
            migrationBuilder.AddCheckConstraint(name: "ck_disease_zones_lifecycle", schema: "plant", table: "disease_zones", sql: "(source_schema_version = 4 AND ((status = 'PROPOSED' AND published_at IS NULL AND published_by IS NULL) OR (status = 'REJECTED' AND current_health_review_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND reviewed_geometry IS NULL AND published_at IS NULL AND published_by IS NULL) OR (status = 'REVIEWED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND has_blocking_items = FALSE AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NULL AND published_by IS NULL))) OR (source_schema_version = 3 AND ((status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)))");
            migrationBuilder.DropTable(
                name: "latest_reviewed_plant_health",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "plant_health_publications",
                schema: "survey");
        }
    }
}

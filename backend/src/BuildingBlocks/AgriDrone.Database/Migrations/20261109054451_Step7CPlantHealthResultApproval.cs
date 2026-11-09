using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step7CPlantHealthResultApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "approval_fingerprint",
                schema: "survey",
                table: "survey_results",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "approval_snapshot",
                schema: "survey",
                table: "survey_results",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_batch_id",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "approved_review_revision",
                schema: "survey",
                table: "survey_results",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_results_approved_batch_id_tenant_id_farm_id_id",
                schema: "survey",
                table: "survey_results",
                columns: new[] { "approved_batch_id", "tenant_id", "farm_id", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_results_approval_snapshot",
                schema: "survey",
                table: "survey_results",
                sql: "(result_nature IS NULL AND approved_batch_id IS NULL AND approved_review_revision IS NULL AND approval_snapshot IS NULL AND approval_fingerprint IS NULL) OR (result_nature IS NOT NULL AND result_nature = 'PRELIMINARY_SUSPECTED' AND ((status = 'PENDING_REVIEW' AND approved_batch_id IS NULL AND approved_review_revision IS NULL AND approval_snapshot IS NULL AND approval_fingerprint IS NULL) OR (status IN ('APPROVED','PUBLISHED') AND service_type = 'PLANT_HEALTH' AND approved_batch_id IS NOT NULL AND active_import_batch_id IS NOT NULL AND approved_batch_id = active_import_batch_id AND approved_review_revision IS NOT NULL AND approved_review_revision = review_revision AND source_mission_id IS NOT NULL AND farm_boundary_version_id IS NOT NULL AND farm_base_map_version_id IS NOT NULL AND approval_snapshot IS NOT NULL AND jsonb_typeof(approval_snapshot) = 'object' AND approval_fingerprint IS NOT NULL AND length(approval_fingerprint) = 64 AND team_label_limitation IS NOT NULL AND length(btrim(team_label_limitation)) > 0)))");

            migrationBuilder.AddForeignKey(
                name: "fk_survey_results_approved_batch_context",
                schema: "survey",
                table: "survey_results",
                columns: new[] { "approved_batch_id", "tenant_id", "farm_id", "id" },
                principalSchema: "survey",
                principalTable: "plant_health_import_batches",
                principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.survey_results WHERE approval_snapshot IS NOT NULL OR approved_batch_id IS NOT NULL)
                    THEN
                        RAISE EXCEPTION 'Step7C contains approved result evidence. Use roll-forward recovery or reconcile/archive explicitly.';
                    END IF;
                END $guard$;
                DROP TRIGGER IF EXISTS trg_plant_health_approval_immutable ON survey.survey_results;
                DROP FUNCTION IF EXISTS survey.guard_plant_health_approval();
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_survey_results_approved_batch_context",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropIndex(
                name: "IX_survey_results_approved_batch_id_tenant_id_farm_id_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_results_approval_snapshot",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "approval_fingerprint",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "approval_snapshot",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "approved_batch_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "approved_review_revision",
                schema: "survey",
                table: "survey_results");
        }
    }
}

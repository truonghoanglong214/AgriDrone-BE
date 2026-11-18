using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step7EPlantHealthCompletionOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM mission.drone_missions m WHERE m.status = 'COMPLETED' AND m.mission_purpose = 'PLANT_HEALTH'
                        AND EXISTS (SELECT 1 FROM mission.ai_processing_jobs j WHERE j.mission_id = m.id
                            AND j.job_type = 'DISEASE_DETECTION' AND j.parameters ? 'SourceVersion')) THEN
                        RAISE EXCEPTION 'Existing V4 Completed Missions require explicit published-outcome reconciliation before Step7E; no completion proof is fabricated.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "source_publication_id",
                schema: "notification",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "health_publication_fingerprint",
                schema: "mission",
                table: "drone_missions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "health_publication_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "health_publication_version",
                schema: "mission",
                table: "drone_missions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "health_published_result_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_notification_health_publication",
                schema: "notification",
                table: "notifications",
                column: "source_publication_id",
                unique: true,
                filter: "source_publication_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_health_publication",
                schema: "notification",
                table: "notifications",
                sql: "source_publication_id IS NULL OR (notification_type = 'PLANT_HEALTH_PUBLISHED' AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND entity_type IS NOT NULL AND entity_type = 'PlantHealthPublication' AND entity_id IS NOT NULL AND entity_id = source_publication_id)");

            migrationBuilder.CreateIndex(
                name: "uq_mission_health_publication",
                schema: "mission",
                table: "drone_missions",
                column: "health_publication_id",
                unique: true,
                filter: "health_publication_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mission_health_publication",
                schema: "mission",
                table: "drone_missions",
                sql: "(health_publication_id IS NULL AND health_published_result_id IS NULL AND health_publication_version IS NULL AND health_publication_fingerprint IS NULL) OR (health_publication_id IS NOT NULL AND health_published_result_id IS NOT NULL AND health_publication_version IS NOT NULL AND health_publication_version > 0 AND health_publication_fingerprint IS NOT NULL AND length(health_publication_fingerprint) = 64 AND status = 'COMPLETED' AND processing_status = 'COMPLETED' AND health_review_handoff_id IS NOT NULL AND health_review_version IS NOT NULL AND health_review_pending = 0 AND health_review_awaiting_field_verification = 0 AND health_review_total = health_review_resolved)");

            migrationBuilder.AddForeignKey(
                name: "fk_mission_health_publication",
                schema: "mission",
                table: "drone_missions",
                column: "health_publication_id",
                principalSchema: "survey",
                principalTable: "plant_health_publications",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION mission.guard_published_plant_health_completion() RETURNS trigger
                LANGUAGE plpgsql AS $guard$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.health_publication_id IS NOT NULL THEN
                            RAISE EXCEPTION 'Published Plant Health Mission evidence cannot be deleted.' USING ERRCODE = '23514';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF TG_OP = 'UPDATE' AND OLD.health_publication_id IS NOT NULL AND (
                        ROW(NEW.health_publication_id, NEW.health_published_result_id, NEW.health_publication_version, NEW.health_publication_fingerprint,
                            NEW.health_review_handoff_id, NEW.health_review_version, NEW.health_review_state, NEW.health_review_total, NEW.health_review_pending,
                            NEW.health_review_awaiting_field_verification, NEW.health_review_resolved, NEW.health_review_changed_at,
                            NEW.status, NEW.processing_status, NEW.tenant_id, NEW.farm_id, NEW.survey_order_id, NEW.source_map_version_id, NEW.farm_boundary_version_id)
                        IS DISTINCT FROM ROW(OLD.health_publication_id, OLD.health_published_result_id, OLD.health_publication_version, OLD.health_publication_fingerprint,
                            OLD.health_review_handoff_id, OLD.health_review_version, OLD.health_review_state, OLD.health_review_total, OLD.health_review_pending,
                            OLD.health_review_awaiting_field_verification, OLD.health_review_resolved, OLD.health_review_changed_at,
                            OLD.status, OLD.processing_status, OLD.tenant_id, OLD.farm_id, OLD.survey_order_id, OLD.source_map_version_id, OLD.farm_boundary_version_id)
                    ) THEN
                        RAISE EXCEPTION 'Published Mission completion context is immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF NEW.health_publication_id IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM survey.plant_health_publications p
                        JOIN survey.survey_results r ON r.id = p.survey_result_id AND r.tenant_id = p.tenant_id AND r.farm_id = p.farm_id
                        JOIN survey.survey_orders o ON o.id = r.survey_order_id AND o.tenant_id = r.tenant_id AND o.farm_id = r.farm_id
                        JOIN survey.plant_health_import_batches b ON b.id = p.batch_id
                        WHERE p.id = NEW.health_publication_id AND p.tenant_id = NEW.tenant_id AND p.farm_id = NEW.farm_id
                          AND p.survey_result_id = NEW.health_published_result_id AND p.publication_version = NEW.health_publication_version
                          AND p.fingerprint = NEW.health_publication_fingerprint AND r.source_mission_id = NEW.id
                          AND o.id = NEW.survey_order_id AND o.status = 'COMPLETED' AND r.status = 'PUBLISHED'
                          AND r.farm_base_map_version_id = NEW.source_map_version_id AND r.farm_boundary_version_id = NEW.farm_boundary_version_id
                          AND b.handoff_id = NEW.health_review_handoff_id AND r.approved_review_revision = NEW.health_review_version
                          AND NEW.health_review_total = jsonb_array_length(b.original_payload->'Findings')
                          AND NEW.health_review_changed_at = p.published_at
                          AND NEW.mission_purpose = 'PLANT_HEALTH' AND NEW.health_review_state = 'Resolved'
                    ) THEN
                        RAISE EXCEPTION 'Mission completion must match a committed published result and its source/review context.' USING ERRCODE = '23514';
                    END IF;
                    IF NEW.status = 'COMPLETED' AND NEW.mission_purpose = 'PLANT_HEALTH' AND NEW.health_publication_id IS NULL
                        AND EXISTS (SELECT 1 FROM mission.ai_processing_jobs j WHERE j.mission_id = NEW.id
                            AND j.job_type = 'DISEASE_DETECTION' AND j.parameters ? 'SourceVersion') THEN
                        RAISE EXCEPTION 'V4 Plant Health Mission cannot complete before a verified publication.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER trg_published_plant_health_completion BEFORE INSERT OR UPDATE OR DELETE ON mission.drone_missions
                    FOR EACH ROW EXECUTE FUNCTION mission.guard_published_plant_health_completion();

                CREATE FUNCTION notification.guard_plant_health_publication_notification() RETURNS trigger
                LANGUAGE plpgsql AS $guard$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.source_publication_id IS NOT NULL THEN
                            RAISE EXCEPTION 'Published notification deduplication evidence cannot be deleted.' USING ERRCODE = '23514';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF TG_OP = 'UPDATE' AND OLD.source_publication_id IS NOT NULL AND
                        ROW(NEW.source_publication_id, NEW.user_id, NEW.tenant_id, NEW.farm_id, NEW.notification_type, NEW.entity_type, NEW.entity_id)
                        IS DISTINCT FROM ROW(OLD.source_publication_id, OLD.user_id, OLD.tenant_id, OLD.farm_id, OLD.notification_type, OLD.entity_type, OLD.entity_id) THEN
                        RAISE EXCEPTION 'Published notification recipient/context is immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF NEW.source_publication_id IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM survey.plant_health_publications p JOIN survey.survey_results r ON r.id = p.survey_result_id
                        WHERE p.id = NEW.source_publication_id AND p.tenant_id = NEW.tenant_id AND p.farm_id = NEW.farm_id
                            AND r.status = 'PUBLISHED' AND r.tenant_id = p.tenant_id AND r.farm_id = p.farm_id
                    ) THEN
                        RAISE EXCEPTION 'Owner notification must reference a published result in the same context.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER trg_plant_health_publication_notification BEFORE INSERT OR UPDATE OR DELETE ON notification.notifications
                    FOR EACH ROW EXECUTE FUNCTION notification.guard_plant_health_publication_notification();

                CREATE FUNCTION survey.guard_published_health_comparison() RETURNS trigger
                LANGUAGE plpgsql AS $guard$
                BEGIN
                    IF ROW(NEW.previous_compatible_order_id, NEW.previous_order_compatibility_policy_version, NEW.previous_order_compatibility_reason)
                        IS DISTINCT FROM ROW(OLD.previous_compatible_order_id, OLD.previous_order_compatibility_policy_version, OLD.previous_order_compatibility_reason)
                        AND OLD.status = 'COMPLETED' AND EXISTS (SELECT 1 FROM survey.survey_results r WHERE r.survey_order_id = OLD.id
                            AND r.status = 'PUBLISHED' AND r.result_nature = 'PRELIMINARY_SUSPECTED') THEN
                        RAISE EXCEPTION 'Published comparison reference and compatibility decision are immutable.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER trg_published_health_comparison BEFORE UPDATE ON survey.survey_orders
                    FOR EACH ROW EXECUTE FUNCTION survey.guard_published_health_comparison();
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_health_publication",
                schema: "notification",
                table: "notifications",
                column: "source_publication_id",
                principalSchema: "survey",
                principalTable: "plant_health_publications",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM mission.drone_missions WHERE health_publication_id IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM notification.notifications WHERE source_publication_id IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM survey.plant_health_publications WHERE payload ? 'Comparison') THEN
                        RAISE EXCEPTION 'Step7E contains completion/notification/comparison evidence. Use roll-forward recovery or reconcile/archive explicitly.';
                    END IF;
                END $guard$;
                DROP TRIGGER trg_published_health_comparison ON survey.survey_orders;
                DROP FUNCTION survey.guard_published_health_comparison();
                DROP TRIGGER trg_plant_health_publication_notification ON notification.notifications;
                DROP FUNCTION notification.guard_plant_health_publication_notification();
                DROP TRIGGER trg_published_plant_health_completion ON mission.drone_missions;
                DROP FUNCTION mission.guard_published_plant_health_completion();
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_mission_health_publication",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_health_publication",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "uq_notification_health_publication",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_health_publication",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "uq_mission_health_publication",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mission_health_publication",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "source_publication_id",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "health_publication_fingerprint",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_publication_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_publication_version",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_published_result_id",
                schema: "mission",
                table: "drone_missions");
        }
    }
}

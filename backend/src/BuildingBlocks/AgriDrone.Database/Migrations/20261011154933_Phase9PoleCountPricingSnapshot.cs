using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase9PoleCountPricingSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_pole_count_snapshot_complete",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.Sql(
                """
                CREATE TYPE system.survey_pole_count_source AS ENUM (
                    'BASELINE_PUBLICATION',
                    'CURRENT_PUBLISHED_INVENTORY');
                """);

            migrationBuilder.AddColumn<string>(
                name: "pole_count_evidence_reference",
                schema: "survey",
                table: "survey_orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pole_count_source",
                schema: "survey",
                table: "survey_orders",
                type: "system.survey_pole_count_source",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE survey.survey_orders AS survey_order
                SET pole_count_source = CASE
                    WHEN base_map.source_survey_order_id = survey_order.id
                    THEN 'BASELINE_PUBLICATION'::system.survey_pole_count_source
                    ELSE 'CURRENT_PUBLISHED_INVENTORY'::system.survey_pole_count_source
                END
                FROM farm.farm_base_map_versions AS base_map
                WHERE survey_order.confirmed_survey_pole_count IS NOT NULL
                  AND base_map.id = survey_order.farm_base_map_version_id
                  AND base_map.tenant_id = survey_order.tenant_id
                  AND base_map.farm_id = survey_order.farm_id;

                DO $guard$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM survey.survey_orders
                        WHERE confirmed_survey_pole_count IS NOT NULL
                          AND pole_count_source IS NULL)
                    THEN
                        RAISE EXCEPTION
                            'Phase 9C migration blocked: a confirmed pole count has no matching base-map source.';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_pole_count_evidence",
                schema: "survey",
                table: "survey_orders",
                sql: "pole_count_evidence_reference IS NULL OR length(btrim(pole_count_evidence_reference)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_pole_count_snapshot_complete",
                schema: "survey",
                table: "survey_orders",
                sql: "(confirmed_survey_pole_count IS NULL AND pole_count_source IS NULL AND pole_count_evidence_reference IS NULL AND pole_count_confirmed_by IS NULL AND pole_count_confirmed_at IS NULL) OR (confirmed_survey_pole_count IS NOT NULL AND farm_boundary_version_id IS NOT NULL AND farm_base_map_version_id IS NOT NULL AND pole_count_source IS NOT NULL AND pole_count_confirmed_by IS NOT NULL AND pole_count_confirmed_at IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_pole_count_evidence",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_pole_count_snapshot_complete",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "pole_count_evidence_reference",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "pole_count_source",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.Sql(
                "DROP TYPE system.survey_pole_count_source;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_pole_count_snapshot_complete",
                schema: "survey",
                table: "survey_orders",
                sql: "(confirmed_survey_pole_count IS NULL AND pole_count_confirmed_by IS NULL AND pole_count_confirmed_at IS NULL) OR (confirmed_survey_pole_count IS NOT NULL AND farm_boundary_version_id IS NOT NULL AND farm_base_map_version_id IS NOT NULL AND pole_count_confirmed_by IS NOT NULL AND pole_count_confirmed_at IS NOT NULL)");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_04MappingAndPlantIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "baseline_map_version_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "count_verified_at",
                schema: "survey",
                table: "survey_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "count_verified_by",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "verified_billable_pole_count",
                schema: "survey",
                table: "survey_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "manager_decision_id",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plant_profile_id",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_survey_order_id",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "spatial_policy_version_id",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "approved_internal_at",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_internal_by",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "baseline_map_version_id",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_baseline",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "predecessor_version_id",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "plant_profiles",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    physical_pole_identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    baseline_location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    current_location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    baseline_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    predecessor_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    replacement_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_profiles", x => x.id);
                    table.CheckConstraint("ck_plant_profiles_predecessor", "(status != 'Replaced') OR (replacement_reason IS NOT NULL)");
                    table.CheckConstraint("ck_plant_profiles_status", "status IN ('Active', 'NeedsReview', 'Retired', 'Replaced')");
                    table.ForeignKey(
                        name: "fk_plant_profiles_predecessor",
                        column: x => x.predecessor_profile_id,
                        principalSchema: "plant",
                        principalTable: "plant_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Physical dragon-fruit pole profile tracking identity, baseline location, and lifecycle lineage.");

            migrationBuilder.CreateTable(
                name: "spatial_policies",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stream_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    boundary_tolerance_m = table.Column<double>(type: "double precision", nullable: false),
                    matching_distance_threshold_m = table.Column<double>(type: "double precision", nullable: false),
                    row_bearing_tolerance_deg = table.Column<double>(type: "double precision", nullable: false),
                    grid_spacing_tolerance_m = table.Column<double>(type: "double precision", nullable: false),
                    policy_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spatial_policies", x => x.id);
                    table.CheckConstraint("ck_spatial_policies_tolerances", "boundary_tolerance_m > 0 AND matching_distance_threshold_m > 0 AND row_bearing_tolerance_deg > 0 AND grid_spacing_tolerance_m > 0");
                    table.CheckConstraint("ck_spatial_policies_version", "version_number >= 1");
                    table.CheckConstraint("ck_spatial_policies_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_spatial_policies_predecessor",
                        column: x => x.predecessor_version_id,
                        principalSchema: "survey",
                        principalTable: "spatial_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_verified_billable_pole_count",
                schema: "survey",
                table: "survey_orders",
                sql: "verified_billable_pole_count IS NULL OR verified_billable_pole_count >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_plant_change_events_plant_profile",
                schema: "plant",
                table: "plant_change_events",
                column: "plant_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_plant_change_events_source_survey_order",
                schema: "plant",
                table: "plant_change_events",
                column: "source_survey_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_farm_base_map_versions_baseline_map_version_id",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "baseline_map_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_farm_base_map_versions_predecessor_version_id",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "predecessor_version_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_farm_base_map_versions_baseline_lineage",
                schema: "farm",
                table: "farm_base_map_versions",
                sql: "(is_baseline = true AND predecessor_version_id IS NULL) OR (is_baseline = false)");

            migrationBuilder.CreateIndex(
                name: "ix_plant_profiles_current_location_gist",
                schema: "plant",
                table: "plant_profiles",
                column: "current_location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_plant_profiles_farm_status",
                schema: "plant",
                table: "plant_profiles",
                columns: new[] { "farm_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_profiles_predecessor",
                schema: "plant",
                table: "plant_profiles",
                column: "predecessor_profile_id");

            migrationBuilder.CreateIndex(
                name: "uq_plant_profiles_active_identifier",
                schema: "plant",
                table: "plant_profiles",
                columns: new[] { "farm_id", "physical_pole_identifier" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_spatial_policies_effective",
                schema: "survey",
                table: "spatial_policies",
                column: "effective_from",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_spatial_policies_predecessor_version_id",
                schema: "survey",
                table: "spatial_policies",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_spatial_policies_stream_version",
                schema: "survey",
                table: "spatial_policies",
                columns: new[] { "stream_key", "version_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_base_map_versions_baseline",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "baseline_map_version_id",
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_base_map_versions_predecessor",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "predecessor_version_id",
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_base_map_versions_baseline",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropForeignKey(
                name: "fk_farm_base_map_versions_predecessor",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropTable(
                name: "plant_profiles",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "spatial_policies",
                schema: "survey");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_verified_billable_pole_count",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropIndex(
                name: "ix_plant_change_events_plant_profile",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropIndex(
                name: "ix_plant_change_events_source_survey_order",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropIndex(
                name: "IX_farm_base_map_versions_baseline_map_version_id",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropIndex(
                name: "IX_farm_base_map_versions_predecessor_version_id",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_farm_base_map_versions_baseline_lineage",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "baseline_map_version_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "count_verified_at",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "count_verified_by",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "verified_billable_pole_count",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "manager_decision_id",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "plant_profile_id",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "source_survey_order_id",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "spatial_policy_version_id",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "approved_internal_at",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "approved_internal_by",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "baseline_map_version_id",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "is_baseline",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "predecessor_version_id",
                schema: "farm",
                table: "farm_base_map_versions");
        }
    }
}

using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_04CanonicalRemediationFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TYPE system.farm_base_map_status ADD VALUE IF NOT EXISTS 'INTERNAL_APPROVED' BEFORE 'PUBLISHED';",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "UPDATE farm.mapping_candidate_batches SET status = 'Applied' WHERE status = 'Published';");

            migrationBuilder.Sql(@"
INSERT INTO survey.spatial_policies (
    id,
    stream_key,
    version_number,
    predecessor_version_id,
    boundary_tolerance_m,
    matching_distance_threshold_m,
    row_bearing_tolerance_deg,
    grid_spacing_tolerance_m,
    policy_fingerprint,
    effective_from,
    effective_to,
    created_by,
    created_at,
    reason_code,
    evidence_reference
)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'DEFAULT',
    1,
    NULL,
    2.0,
    1.5,
    15.0,
    0.5,
    '317f7abead50264ef86b76f3d6be8a4d781262da5e7911e06be8d95f859a5e61',
    '2026-01-01 00:00:00+00',
    NULL,
    '00000000-0000-0000-0000-000000000001',
    '2026-01-01 00:00:00+00',
    'INITIAL_DEFAULT_POLICY',
    'System initialization default'
)
ON CONFLICT (id) DO NOTHING;");

            migrationBuilder.DropForeignKey(
                name: "fk_farm_baseline_pointers_map_version",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_profile_observations_profile",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_appointments_confirmation",
                schema: "survey",
                table: "survey_appointments");

            migrationBuilder.DropPrimaryKey(
                name: "pk_farm_baseline_pointers",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_farm_baseline_pointers_farm_tenant",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropIndex(
                name: "IX_farm_baseline_pointers_current_baseline_map_version_id",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "farm",
                table: "farm_baseline_pointers",
                newName: "id");

            migrationBuilder.AddColumn<decimal>(
                name: "mapping_price_per_pole",
                schema: "survey",
                table: "survey_service_prices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "plant_health_price_per_pole",
                schema: "survey",
                table: "survey_service_prices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "booked_mapping_price_per_pole",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "booked_plant_health_price_per_pole",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "booked_price_per_pole",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "booked_service_price_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<SurveyServiceType>(
                name: "booked_service_type",
                schema: "survey",
                table: "survey_orders",
                type: "system.survey_service_type",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "combo_discount_amount",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "confirmed_mapping_pole_count",
                schema: "survey",
                table: "survey_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "confirmed_plant_health_pole_count",
                schema: "survey",
                table: "survey_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mapping_subtotal",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "plant_health_subtotal",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "post_flight_settlement",
                schema: "survey",
                table: "survey_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "service_reviewed_at",
                schema: "survey",
                table: "survey_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "mapping_candidate_batch_id",
                schema: "plant",
                table: "plant_profile_observations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "boundary_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "grid_spacing_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "mapping_kind",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "matching_distance_threshold_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "row_bearing_tolerance_deg",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "spatial_policy_fingerprint",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "spatial_policy_version_id",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "uq_survey_order_pole_count_snapshots_id_tenant_farm",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "id", "tenant_id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_plant_profiles_id_farm",
                schema: "plant",
                table: "plant_profiles",
                columns: new[] { "id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_plant_profiles_id_tenant_farm",
                schema: "plant",
                table: "plant_profiles",
                columns: new[] { "id", "tenant_id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_mapping_candidates_id_batch",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "id", "batch_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_mapping_candidate_batches_id_tenant_farm",
                schema: "farm",
                table: "mapping_candidate_batches",
                columns: new[] { "id", "tenant_id", "farm_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_farm_baseline_pointers",
                schema: "farm",
                table: "farm_baseline_pointers",
                column: "id");

            migrationBuilder.CreateTable(
                name: "survey_order_pole_count_snapshot_items",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_candidate_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_position = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    review_decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    is_strictly_in_boundary = table.Column<bool>(type: "boolean", nullable: false),
                    exclusion_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_order_pole_count_snapshot_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshot_items_batch",
                        columns: x => new { x.mapping_candidate_batch_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshot_items_candidate",
                        columns: x => new { x.mapping_candidate_id, x.mapping_candidate_batch_id },
                        principalSchema: "farm",
                        principalTable: "mapping_candidates",
                        principalColumns: new[] { "id", "batch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshot_items_snapshot",
                        column: x => x.snapshot_id,
                        principalSchema: "survey",
                        principalTable: "survey_order_pole_count_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_service_prices_combo_components",
                schema: "survey",
                table: "survey_service_prices",
                sql: "(mapping_price_per_pole IS NULL AND plant_health_price_per_pole IS NULL) OR (mapping_price_per_pole IS NOT NULL AND plant_health_price_per_pole IS NOT NULL AND mapping_price_per_pole > 0 AND plant_health_price_per_pole > 0 AND mapping_price_per_pole = trunc(mapping_price_per_pole) AND plant_health_price_per_pole = trunc(plant_health_price_per_pole) AND price_per_pole IS NOT NULL AND price_per_pole = mapping_price_per_pole + plant_health_price_per_pole)");

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_verified_pole_count_snapshot_id",
                schema: "survey",
                table: "survey_orders",
                column: "verified_pole_count_snapshot_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_combo_component_booking",
                schema: "survey",
                table: "survey_orders",
                sql: "(booked_mapping_price_per_pole IS NULL AND booked_plant_health_price_per_pole IS NULL) OR (booked_mapping_price_per_pole IS NOT NULL AND booked_plant_health_price_per_pole IS NOT NULL AND scope = 'LEGACY' AND post_flight_settlement AND booked_service_price_id IS NOT NULL AND booked_price_per_pole IS NOT NULL AND booked_mapping_price_per_pole > 0 AND booked_plant_health_price_per_pole > 0 AND booked_mapping_price_per_pole = trunc(booked_mapping_price_per_pole) AND booked_plant_health_price_per_pole = trunc(booked_plant_health_price_per_pole) AND booked_price_per_pole = booked_mapping_price_per_pole + booked_plant_health_price_per_pole)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_combo_component_invoice",
                schema: "survey",
                table: "survey_orders",
                sql: "(confirmed_mapping_pole_count IS NULL AND confirmed_plant_health_pole_count IS NULL AND mapping_subtotal IS NULL AND plant_health_subtotal IS NULL AND combo_discount_amount IS NULL AND (booked_mapping_price_per_pole IS NULL OR final_price IS NULL)) OR (booked_mapping_price_per_pole IS NOT NULL AND booked_plant_health_price_per_pole IS NOT NULL AND confirmed_mapping_pole_count IS NOT NULL AND confirmed_plant_health_pole_count IS NOT NULL AND confirmed_mapping_pole_count > 0 AND confirmed_plant_health_pole_count > 0 AND confirmed_plant_health_pole_count <= confirmed_mapping_pole_count AND mapping_subtotal IS NOT NULL AND plant_health_subtotal IS NOT NULL AND combo_discount_amount IS NOT NULL AND combo_discount_percentage IS NOT NULL AND mapping_subtotal = confirmed_mapping_pole_count * booked_mapping_price_per_pole AND plant_health_subtotal = confirmed_plant_health_pole_count * booked_plant_health_price_per_pole AND final_price IS NOT NULL AND final_price = round((mapping_subtotal + plant_health_subtotal) * (1 - combo_discount_percentage / 100), 0) AND combo_discount_amount = mapping_subtotal + plant_health_subtotal - final_price AND original_final_price IS NOT NULL AND original_final_price = mapping_subtotal + plant_health_subtotal AND original_survey_service_price_id IS NOT NULL AND original_survey_service_price_id = booked_service_price_id AND confirmed_survey_pole_count IS NOT NULL AND confirmed_survey_pole_count = confirmed_plant_health_pole_count)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders",
                sql: "booked_mapping_price_per_pole IS NOT NULL OR scope = 'V3' OR final_price IS NULL OR (price_per_pole_snapshot IS NOT NULL AND final_price = round(confirmed_survey_pole_count * price_per_pole_snapshot, 2)) OR (price_per_ha_snapshot IS NOT NULL AND final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders",
                sql: "booked_mapping_price_per_pole IS NOT NULL OR (original_final_price IS NULL AND original_survey_service_price_id IS NULL AND original_confirmed_pole_count IS NULL AND original_price_per_pole_snapshot IS NULL) OR (original_final_price = round(original_confirmed_pole_count * original_price_per_pole_snapshot, 2) AND original_survey_service_price_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshots_farm_base_map_version_id_~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshots_farm_boundary_version_id_~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshots_spatial_policy_version_id",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                column: "spatial_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshots_survey_order_id_tenant_id~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_order_pole_count_snapshots_order_fingerprint",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "survey_order_id", "evidence_fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_appointments_confirmed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments",
                column: "confirmed_by_system_manager_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_appointments_confirmation",
                schema: "survey",
                table: "survey_appointments",
                sql: "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NOT NULL AND ((confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_by_system_manager_id IS NULL) OR (purpose = 'PAID_SERVICE'::system.survey_appointment_purpose AND confirmed_by_tenant_owner_id IS NULL AND confirmed_by_system_manager_id IS NOT NULL))) OR (status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL AND confirmed_by_system_manager_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_plants_plant_profile_id_farm_id",
                schema: "plant",
                table: "plants",
                columns: new[] { "plant_profile_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_profile_observations_farm_base_map_version_id_tenant_~",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_profile_observations_plant_profile_id_tenant_id_farm_~",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "plant_profile_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_profile_observations_survey_order_id_tenant_id_farm_id",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_profile_observations_distance",
                schema: "plant",
                table: "plant_profile_observations",
                sql: "observed_distance_m >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_farm_baseline_pointers_current_baseline_map_version_id_tena~",
                schema: "farm",
                table: "farm_baseline_pointers",
                columns: new[] { "current_baseline_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_farm_baseline_pointers_farm_id_tenant_id",
                schema: "farm",
                table: "farm_baseline_pointers",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "uq_farm_baseline_pointers_farm",
                schema: "farm",
                table: "farm_baseline_pointers",
                column: "farm_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_farm_baseline_pointers_tenant_farm",
                schema: "farm",
                table: "farm_baseline_pointers",
                columns: new[] { "tenant_id", "farm_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshot_items_mapping_candidate_ba~",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "mapping_candidate_batch_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshot_items_mapping_candidate_id~",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "mapping_candidate_id", "mapping_candidate_batch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_order_pole_count_snapshot_items_pos_gist",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                column: "effective_position")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "uq_survey_order_pole_count_snapshot_items_snapshot_candidate",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "snapshot_id", "mapping_candidate_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_baseline_pointers_map_version",
                schema: "farm",
                table: "farm_baseline_pointers",
                columns: new[] { "current_baseline_map_version_id", "tenant_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_profile_observations_base_map_version",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_profile_observations_profile_same_scope",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "plant_profile_id", "tenant_id", "farm_id" },
                principalSchema: "plant",
                principalTable: "plant_profiles",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_profile_observations_survey_order",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_orders",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plants_plant_profile_same_farm",
                schema: "plant",
                table: "plants",
                columns: new[] { "plant_profile_id", "farm_id" },
                principalSchema: "plant",
                principalTable: "plant_profiles",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_appointments_users_confirmed_by_manager",
                schema: "survey",
                table: "survey_appointments",
                column: "confirmed_by_system_manager_id",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_order_pole_count_snapshots_base_map",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_order_pole_count_snapshots_boundary",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_boundaries",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_order_pole_count_snapshots_order",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_orders",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_order_pole_count_snapshots_spatial_policy",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                column: "spatial_policy_version_id",
                principalSchema: "survey",
                principalTable: "spatial_policies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_orders_pole_count_snapshot",
                schema: "survey",
                table: "survey_orders",
                column: "verified_pole_count_snapshot_id",
                principalSchema: "survey",
                principalTable: "survey_order_pole_count_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_baseline_pointers_map_version",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_profile_observations_base_map_version",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_profile_observations_profile_same_scope",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_profile_observations_survey_order",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropForeignKey(
                name: "fk_plants_plant_profile_same_farm",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_appointments_users_confirmed_by_manager",
                schema: "survey",
                table: "survey_appointments");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_order_pole_count_snapshots_base_map",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_order_pole_count_snapshots_boundary",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_order_pole_count_snapshots_order",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_order_pole_count_snapshots_spatial_policy",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_orders_pole_count_snapshot",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropTable(
                name: "survey_order_pole_count_snapshot_items",
                schema: "survey");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_service_prices_combo_components",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropIndex(
                name: "IX_survey_orders_verified_pole_count_snapshot_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_combo_component_booking",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_combo_component_invoice",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_survey_order_pole_count_snapshots_id_tenant_farm",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_survey_order_pole_count_snapshots_farm_base_map_version_id_~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_survey_order_pole_count_snapshots_farm_boundary_version_id_~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_survey_order_pole_count_snapshots_spatial_policy_version_id",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_survey_order_pole_count_snapshots_survey_order_id_tenant_id~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "uq_survey_order_pole_count_snapshots_order_fingerprint",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_survey_appointments_confirmed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_appointments_confirmation",
                schema: "survey",
                table: "survey_appointments");

            migrationBuilder.DropIndex(
                name: "IX_plants_plant_profile_id_farm_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_plant_profiles_id_farm",
                schema: "plant",
                table: "plant_profiles");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_plant_profiles_id_tenant_farm",
                schema: "plant",
                table: "plant_profiles");

            migrationBuilder.DropIndex(
                name: "IX_plant_profile_observations_farm_base_map_version_id_tenant_~",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropIndex(
                name: "IX_plant_profile_observations_plant_profile_id_tenant_id_farm_~",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropIndex(
                name: "IX_plant_profile_observations_survey_order_id_tenant_id_farm_id",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_profile_observations_distance",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_mapping_candidates_id_batch",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_mapping_candidate_batches_id_tenant_farm",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropPrimaryKey(
                name: "pk_farm_baseline_pointers",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropIndex(
                name: "IX_farm_baseline_pointers_current_baseline_map_version_id_tena~",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropIndex(
                name: "IX_farm_baseline_pointers_farm_id_tenant_id",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropIndex(
                name: "uq_farm_baseline_pointers_farm",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropIndex(
                name: "uq_farm_baseline_pointers_tenant_farm",
                schema: "farm",
                table: "farm_baseline_pointers");

            migrationBuilder.DropColumn(
                name: "mapping_price_per_pole",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "plant_health_price_per_pole",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "booked_mapping_price_per_pole",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_plant_health_price_per_pole",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_price_per_pole",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_service_price_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_service_type",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "combo_discount_amount",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "confirmed_mapping_pole_count",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "confirmed_plant_health_pole_count",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "mapping_subtotal",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "plant_health_subtotal",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "post_flight_settlement",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "service_reviewed_at",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "confirmed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments");

            migrationBuilder.DropColumn(
                name: "mapping_candidate_batch_id",
                schema: "plant",
                table: "plant_profile_observations");

            migrationBuilder.DropColumn(
                name: "boundary_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "grid_spacing_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "mapping_kind",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "matching_distance_threshold_m",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "row_bearing_tolerance_deg",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "spatial_policy_fingerprint",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "spatial_policy_version_id",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "farm",
                table: "farm_baseline_pointers",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_farm_baseline_pointers",
                schema: "farm",
                table: "farm_baseline_pointers",
                column: "farm_id");

            migrationBuilder.AddUniqueConstraint(
                name: "uq_farm_baseline_pointers_farm_tenant",
                schema: "farm",
                table: "farm_baseline_pointers",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_final_price",
                schema: "survey",
                table: "survey_orders",
                sql: "scope = 'V3' OR final_price IS NULL OR (price_per_pole_snapshot IS NOT NULL AND final_price = round(confirmed_survey_pole_count * price_per_pole_snapshot, 2)) OR (price_per_ha_snapshot IS NOT NULL AND final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_original_commercial_snapshot",
                schema: "survey",
                table: "survey_orders",
                sql: "(original_final_price IS NULL AND original_survey_service_price_id IS NULL AND original_confirmed_pole_count IS NULL AND original_price_per_pole_snapshot IS NULL) OR (original_final_price = round(original_confirmed_pole_count * original_price_per_pole_snapshot, 2) AND original_survey_service_price_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_appointments_confirmation",
                schema: "survey",
                table: "survey_appointments",
                sql: "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_at IS NOT NULL) OR (status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_farm_baseline_pointers_current_baseline_map_version_id",
                schema: "farm",
                table: "farm_baseline_pointers",
                column: "current_baseline_map_version_id");

            migrationBuilder.AddForeignKey(
                name: "fk_farm_baseline_pointers_map_version",
                schema: "farm",
                table: "farm_baseline_pointers",
                column: "current_baseline_map_version_id",
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_profile_observations_profile",
                schema: "plant",
                table: "plant_profile_observations",
                column: "plant_profile_id",
                principalSchema: "plant",
                principalTable: "plant_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

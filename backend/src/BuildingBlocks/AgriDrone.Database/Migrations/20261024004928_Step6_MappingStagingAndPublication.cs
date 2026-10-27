using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step6_MappingStagingAndPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "replaced_plant_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_boundary_version_id",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "handoff_id",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_mission_id",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mapping_candidate_batches",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_current_farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    algorithm_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    boundary_policy_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    produced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_candidate_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapping_candidate_batches_farms",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mapping_candidate_zones",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_current_zone_map_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grid_bearing_deg = table.Column<double>(type: "double precision", nullable: false),
                    row_spacing_m = table.Column<double>(type: "double precision", nullable: false),
                    plant_spacing_m = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_candidate_zones", x => x.id);
                    table.UniqueConstraint("AK_mapping_candidate_zones_zone_id", x => x.zone_id);
                    table.ForeignKey(
                        name: "fk_mapping_candidate_zones_batch",
                        column: x => x.batch_id,
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mapping_candidates",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_media_asset_ids = table.Column<string>(type: "jsonb", nullable: false),
                    original_position = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    proposed_row_index = table.Column<int>(type: "integer", nullable: true),
                    proposed_column_index = table.Column<int>(type: "integer", nullable: true),
                    position_confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    boundary_classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    measured_boundary_distance_m = table.Column<decimal>(type: "numeric(8,3)", nullable: false),
                    boundary_threshold_m = table.Column<decimal>(type: "numeric(8,3)", nullable: false),
                    proposed_matched_plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    match_confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    review_decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    corrected_position = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true),
                    corrected_row_index = table.Column<int>(type: "integer", nullable: true),
                    corrected_column_index = table.Column<int>(type: "integer", nullable: true),
                    corrected_matched_plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapping_candidates_batch",
                        column: x => x.batch_id,
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mapping_candidates_zone",
                        column: x => x.zone_id,
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_zones",
                        principalColumn: "zone_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plants_replaced_plant_id",
                schema: "plant",
                table: "plants",
                column: "replaced_plant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mapping_candidate_batches_farm_id_tenant_id",
                schema: "farm",
                table: "mapping_candidate_batches",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_mapping_candidate_batches_farm_order",
                schema: "farm",
                table: "mapping_candidate_batches",
                columns: new[] { "farm_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "uq_mapping_candidate_batches_handoff",
                schema: "farm",
                table: "mapping_candidate_batches",
                column: "handoff_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_mapping_candidate_zones_batch_zone",
                schema: "farm",
                table: "mapping_candidate_zones",
                columns: new[] { "batch_id", "zone_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_candidates_original_position_gist",
                schema: "farm",
                table: "mapping_candidates",
                column: "original_position")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_mapping_candidates_zone_id",
                schema: "farm",
                table: "mapping_candidates",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "uq_mapping_candidates_batch_candidate",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "batch_id", "candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_mapping_candidates_batch_observation",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "batch_id", "source_observation_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_plants_replaced_plant_id",
                schema: "plant",
                table: "plants",
                column: "replaced_plant_id",
                principalSchema: "plant",
                principalTable: "plants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_plants_replaced_plant_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropTable(
                name: "mapping_candidates",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "mapping_candidate_zones",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "mapping_candidate_batches",
                schema: "farm");

            migrationBuilder.DropIndex(
                name: "IX_plants_replaced_plant_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "replaced_plant_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "farm_boundary_version_id",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "handoff_id",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "source_mission_id",
                schema: "farm",
                table: "farm_base_map_versions");
        }
    }
}

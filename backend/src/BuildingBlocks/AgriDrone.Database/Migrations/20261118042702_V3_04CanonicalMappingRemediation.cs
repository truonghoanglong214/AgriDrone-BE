using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_04CanonicalMappingRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_farm_base_map_versions_farm_version",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropIndex(
                name: "uq_farm_base_map_versions_one_published",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_farm_base_map_versions_baseline_lineage",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_farm_base_map_versions_publication",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.AddColumn<Guid>(
                name: "verified_pole_count_snapshot_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plant_profile_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "source_observation_id",
                schema: "farm",
                table: "mapping_candidates",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<double>(
                name: "boundary_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "capture_attempt_id",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "grid_spacing_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mapping_kind",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "matching_distance_threshold_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payload_fingerprint",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "row_bearing_tolerance_deg",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "spatial_policy_fingerprint",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "spatial_policy_version_id",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "internally_approved_map_version_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "map_internally_approved_at",
                schema: "mission",
                table: "drone_missions",
                type: "timestamp with time zone",
                nullable: true);

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

            migrationBuilder.CreateTable(
                name: "farm_baseline_pointers",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_baseline_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_baseline_pointers", x => x.id);
                    table.ForeignKey(
                        name: "fk_farm_baseline_pointers_farms",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_baseline_pointers_map_version",
                        columns: x => new { x.current_baseline_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_profile_observations",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plant_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_candidate_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observed_location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    observed_distance_m = table.Column<double>(type: "double precision", nullable: false),
                    observed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_profile_observations", x => x.id);
                    table.CheckConstraint("ck_plant_profile_observations_distance", "observed_distance_m >= 0");
                    table.ForeignKey(
                        name: "fk_plant_profile_observations_base_map_version",
                        columns: x => new { x.farm_base_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plant_profile_observations_candidate",
                        columns: x => new { x.candidate_id, x.mapping_candidate_batch_id },
                        principalSchema: "farm",
                        principalTable: "mapping_candidates",
                        principalColumns: new[] { "id", "batch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plant_profile_observations_profile_same_scope",
                        columns: x => new { x.plant_profile_id, x.tenant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plant_profiles",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plant_profile_observations_survey_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_order_pole_count_snapshots",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_candidate_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spatial_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_billable_pole_count = table.Column<int>(type: "integer", nullable: false),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    evidence_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_order_pole_count_snapshots", x => x.id);
                    table.UniqueConstraint("uq_survey_order_pole_count_snapshots_id_tenant_farm", x => new { x.id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_survey_order_pole_count_snapshots_count", "verified_billable_pole_count >= 0");
                    table.CheckConstraint("ck_survey_order_pole_count_snapshots_rev", "revision_number >= 1");
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshots_base_map",
                        columns: x => new { x.farm_base_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshots_batch_same_tenant_farm",
                        columns: x => new { x.mapping_candidate_batch_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshots_boundary",
                        columns: x => new { x.farm_boundary_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_boundaries",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshots_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshots_spatial_policy",
                        column: x => x.spatial_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "spatial_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    source_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_position = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    review_decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    is_strictly_in_boundary = table.Column<bool>(type: "boolean", nullable: false),
                    exclusion_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    target_plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_plant_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_position = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true),
                    effective_row_index = table.Column<int>(type: "integer", nullable: true),
                    effective_column_index = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_order_pole_count_snapshot_items", x => x.id);
                    table.CheckConstraint("ck_survey_order_pole_count_snapshot_items_acceptance", "(is_accepted = TRUE AND plant_profile_id IS NOT NULL AND exclusion_reason IS NULL) OR (is_accepted = FALSE AND plant_profile_id IS NULL AND exclusion_reason IS NOT NULL AND btrim(exclusion_reason) <> '')");
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
                        name: "fk_survey_order_pole_count_snapshot_items_profile",
                        columns: x => new { x.plant_profile_id, x.tenant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plant_profiles",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshot_items_snapshot",
                        columns: x => new { x.snapshot_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_order_pole_count_snapshots",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_order_pole_count_snapshot_items_target_profile",
                        columns: x => new { x.target_plant_profile_id, x.tenant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plant_profiles",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Existing batches must receive real provenance. No Guid.Empty, zero tolerance,
            // empty string, fixed policy, or mission-as-attempt fallback is permitted.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    v_invalid_count integer;
                BEGIN
                    WITH matching_attempts AS (
                        SELECT
                            b.id AS batch_id,
                            ca.id AS capture_attempt_id,
                            COUNT(*) OVER (PARTITION BY b.id) AS match_count
                        FROM farm.mapping_candidate_batches b
                        JOIN mission.drone_missions dm
                          ON dm.id = b.mission_id
                         AND dm.survey_order_id = b.survey_order_id
                         AND dm.tenant_id = b.tenant_id
                         AND dm.farm_id = b.farm_id
                         AND dm.capture_attempt_id IS NOT NULL
                        JOIN survey.capture_attempts ca
                          ON ca.id = dm.capture_attempt_id
                         AND ca.order_id = b.survey_order_id
                         AND ca.tenant_id = b.tenant_id
                         AND ca.farm_id = b.farm_id
                    )
                    UPDATE farm.mapping_candidate_batches b
                    SET capture_attempt_id = m.capture_attempt_id
                    FROM matching_attempts m
                    WHERE m.batch_id = b.id
                      AND m.match_count = 1;

                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM farm.mapping_candidate_batches b
                    WHERE b.capture_attempt_id IS NULL
                       OR NOT EXISTS (
                            SELECT 1
                            FROM mission.drone_missions dm
                            JOIN survey.capture_attempts ca
                              ON ca.id = dm.capture_attempt_id
                             AND ca.order_id = b.survey_order_id
                             AND ca.tenant_id = b.tenant_id
                             AND ca.farm_id = b.farm_id
                            WHERE dm.id = b.mission_id
                              AND dm.survey_order_id = b.survey_order_id
                              AND dm.tenant_id = b.tenant_id
                              AND dm.farm_id = b.farm_id
                              AND ca.id = b.capture_attempt_id
                       );

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % mapping batches have no unique capture attempt in the same order/mission/tenant/farm scope.', v_invalid_count;
                    END IF;

                    WITH classified_batches AS (
                        SELECT
                            b.id AS batch_id,
                            CASE
                                WHEN so.requires_baseline_mapping = TRUE AND mv.is_baseline = TRUE
                                    THEN 'FirstBaseline'
                                WHEN so.requires_baseline_mapping = FALSE AND mv.is_baseline = FALSE
                                    THEN 'FollowUpSnapshot'
                                ELSE NULL
                            END AS mapping_kind
                        FROM farm.mapping_candidate_batches b
                        JOIN survey.survey_orders so
                          ON so.id = b.survey_order_id
                         AND so.tenant_id = b.tenant_id
                         AND so.farm_id = b.farm_id
                        LEFT JOIN farm.farm_base_map_versions mv
                          ON mv.id = b.farm_base_map_version_id
                         AND mv.tenant_id = b.tenant_id
                         AND mv.farm_id = b.farm_id
                    )
                    UPDATE farm.mapping_candidate_batches b
                    SET mapping_kind = c.mapping_kind
                    FROM classified_batches c
                    WHERE c.batch_id = b.id;

                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM farm.mapping_candidate_batches
                    WHERE mapping_kind IS NULL
                       OR mapping_kind NOT IN ('FirstBaseline', 'FollowUpSnapshot');

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % mapping batches have indeterminate mapping kind.', v_invalid_count;
                    END IF;

                    WITH selected_policies AS (
                        SELECT
                            b.id AS batch_id,
                            p.id AS policy_id,
                            p.policy_fingerprint,
                            p.boundary_tolerance_m,
                            p.matching_distance_threshold_m,
                            p.row_bearing_tolerance_deg,
                            p.grid_spacing_tolerance_m
                        FROM farm.mapping_candidate_batches b
                        CROSS JOIN LATERAL (
                            SELECT
                                sp.id,
                                sp.policy_fingerprint,
                                sp.boundary_tolerance_m,
                                sp.matching_distance_threshold_m,
                                sp.row_bearing_tolerance_deg,
                                sp.grid_spacing_tolerance_m
                            FROM survey.spatial_policies sp
                            WHERE sp.stream_key = 'DEFAULT'
                              AND sp.effective_from <= b.produced_at
                              AND (sp.effective_to IS NULL OR sp.effective_to > b.produced_at)
                            ORDER BY sp.version_number DESC
                            LIMIT 1
                        ) p
                    )
                    UPDATE farm.mapping_candidate_batches b
                    SET spatial_policy_version_id = p.policy_id,
                        spatial_policy_fingerprint = p.policy_fingerprint,
                        boundary_tolerance_m = p.boundary_tolerance_m,
                        matching_distance_threshold_m = p.matching_distance_threshold_m,
                        row_bearing_tolerance_deg = p.row_bearing_tolerance_deg,
                        grid_spacing_tolerance_m = p.grid_spacing_tolerance_m
                    FROM selected_policies p
                    WHERE p.batch_id = b.id;

                    UPDATE farm.mapping_candidate_batches b
                    SET payload_fingerprint = encode(
                        digest(
                            concat_ws(
                                '|',
                                b.handoff_id::text,
                                b.survey_order_id::text,
                                b.mission_id::text,
                                b.capture_attempt_id::text,
                                b.job_id::text),
                            'sha256'),
                        'hex');

                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM farm.mapping_candidate_batches
                    WHERE capture_attempt_id IS NULL
                       OR capture_attempt_id = '00000000-0000-0000-0000-000000000000'::uuid
                       OR spatial_policy_version_id IS NULL
                       OR spatial_policy_version_id = '00000000-0000-0000-0000-000000000000'::uuid
                       OR boundary_tolerance_m IS NULL OR boundary_tolerance_m <= 0
                       OR matching_distance_threshold_m IS NULL OR matching_distance_threshold_m <= 0
                       OR row_bearing_tolerance_deg IS NULL OR row_bearing_tolerance_deg <= 0
                       OR grid_spacing_tolerance_m IS NULL OR grid_spacing_tolerance_m <= 0
                       OR payload_fingerprint IS NULL OR payload_fingerprint !~ '^[0-9a-f]{64}$'
                       OR spatial_policy_fingerprint IS NULL OR spatial_policy_fingerprint !~ '^[0-9a-f]{64}$';

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % mapping batches failed provenance/policy validation.', v_invalid_count;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<double>(
                name: "boundary_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "capture_attempt_id",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "grid_spacing_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "mapping_kind",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "matching_distance_threshold_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "payload_fingerprint",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "row_bearing_tolerance_deg",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "spatial_policy_fingerprint",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "spatial_policy_version_id",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    v_invalid_count integer;
                BEGIN
                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM (
                        SELECT tenant_id, farm_id
                        FROM farm.farm_base_map_versions
                        WHERE is_baseline = TRUE
                        GROUP BY tenant_id, farm_id
                        HAVING COUNT(*) > 1
                    ) duplicates;

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % tenant/farm scopes contain multiple baseline maps.', v_invalid_count;
                    END IF;

                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM (
                        SELECT tenant_id, farm_id
                        FROM farm.farm_base_map_versions
                        WHERE status = 'DRAFT'::system.farm_base_map_status
                        GROUP BY tenant_id, farm_id
                        HAVING COUNT(*) > 1
                    ) duplicates;

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % tenant/farm scopes contain multiple open draft maps.', v_invalid_count;
                    END IF;

                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM (
                        SELECT farm_id, plant_profile_id
                        FROM plant.plants
                        WHERE plant_profile_id IS NOT NULL
                        GROUP BY farm_id, plant_profile_id
                        HAVING COUNT(*) > 1
                    ) duplicates;

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % farm/profile pairs are assigned to multiple plants.', v_invalid_count;
                    END IF;

                    SELECT COUNT(*)
                    INTO v_invalid_count
                    FROM farm.farm_base_map_versions
                    WHERE is_baseline = TRUE
                      AND status IN (
                          'INTERNAL_APPROVED'::system.farm_base_map_status,
                          'PUBLISHED'::system.farm_base_map_status)
                      AND COALESCE(approved_internal_by, published_by) IS NULL;

                    IF v_invalid_count > 0 THEN
                        RAISE EXCEPTION 'V3-04: % accepted baseline maps have no accountable approver/publisher.', v_invalid_count;
                    END IF;

                    INSERT INTO farm.farm_baseline_pointers (
                        id,
                        farm_id,
                        tenant_id,
                        current_baseline_map_version_id,
                        updated_by,
                        updated_at)
                    SELECT
                        m.farm_id,
                        m.farm_id,
                        m.tenant_id,
                        m.id,
                        COALESCE(m.approved_internal_by, m.published_by),
                        COALESCE(m.approved_internal_at, m.published_at, m.updated_at, m.created_at)
                    FROM farm.farm_base_map_versions m
                    WHERE m.is_baseline = TRUE
                      AND m.status IN (
                          'INTERNAL_APPROVED'::system.farm_base_map_status,
                          'PUBLISHED'::system.farm_base_map_status);
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_verified_pole_count_snapshot_id",
                schema: "survey",
                table: "survey_orders",
                column: "verified_pole_count_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_plants_plant_profile",
                schema: "plant",
                table: "plants",
                column: "plant_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_plants_plant_profile_id_farm_id",
                schema: "plant",
                table: "plants",
                columns: new[] { "plant_profile_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_plants_farm_plant_profile",
                schema: "plant",
                table: "plants",
                columns: new[] { "farm_id", "plant_profile_id" },
                unique: true,
                filter: "plant_profile_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_candidate_batches_capture_attempt_mission",
                schema: "farm",
                table: "mapping_candidate_batches",
                columns: new[] { "capture_attempt_id", "mission_id" });

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_one_baseline",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "tenant_id", "farm_id" },
                unique: true,
                filter: "is_baseline = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_one_draft",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "tenant_id", "farm_id" },
                unique: true,
                filter: "status = 'DRAFT'::system.farm_base_map_status");

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_one_published",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "tenant_id", "farm_id" },
                unique: true,
                filter: "status = 'PUBLISHED'::system.farm_base_map_status");

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_tenant_farm_version",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "tenant_id", "farm_id", "version_number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_farm_base_map_versions_baseline_lineage",
                schema: "farm",
                table: "farm_base_map_versions",
                sql: "(is_baseline = TRUE AND version_number = 1 AND predecessor_version_id IS NULL AND baseline_map_version_id = id) OR (is_baseline = FALSE AND version_number > 1 AND predecessor_version_id IS NOT NULL AND baseline_map_version_id IS NOT NULL AND baseline_map_version_id <> id)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_farm_base_map_versions_publication",
                schema: "farm",
                table: "farm_base_map_versions",
                sql: "(status IN ('DRAFT'::system.farm_base_map_status, 'INTERNAL_APPROVED'::system.farm_base_map_status) AND published_by IS NULL AND published_at IS NULL) OR (status IN ('PUBLISHED'::system.farm_base_map_status, 'SUPERSEDED'::system.farm_base_map_status) AND published_by IS NOT NULL AND published_at IS NOT NULL)");

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
                name: "IX_plant_profile_observations_candidate_id_mapping_candidate_b~",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "candidate_id", "mapping_candidate_batch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_profile_observations_farm_base_map_version_id_tenant_~",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_profile_observations_farm_order",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "farm_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_profile_observations_plant_profile_id_tenant_id_farm_~",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "plant_profile_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_profile_observations_profile",
                schema: "plant",
                table: "plant_profile_observations",
                column: "plant_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_profile_observations_survey_order_id_tenant_id_farm_id",
                schema: "plant",
                table: "plant_profile_observations",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

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
                name: "IX_survey_order_pole_count_snapshot_items_plant_profile_id_ten~",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "plant_profile_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_order_pole_count_snapshot_items_pos_gist",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                column: "effective_position")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshot_items_snapshot_id_tenant_i~",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "snapshot_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_pole_count_snapshot_items_target_plant_profile~",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "target_plant_profile_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_order_pole_count_snapshot_items_snapshot_candidate",
                schema: "survey",
                table: "survey_order_pole_count_snapshot_items",
                columns: new[] { "snapshot_id", "mapping_candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_order_pole_count_snapshots_batch",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                column: "mapping_candidate_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_order_pole_count_snapshots_farm",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                column: "farm_id");

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
                name: "IX_survey_order_pole_count_snapshots_mapping_candidate_batch_i~",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "mapping_candidate_batch_id", "tenant_id", "farm_id" });

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
                name: "uq_survey_order_pole_count_snapshots_order_revision",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "survey_order_id", "revision_number" },
                unique: true);

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
                name: "fk_plants_plant_profile_same_farm",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_orders_pole_count_snapshot",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropTable(
                name: "farm_baseline_pointers",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "plant_profile_observations",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "survey_order_pole_count_snapshot_items",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_order_pole_count_snapshots",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "IX_survey_orders_verified_pole_count_snapshot_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropIndex(
                name: "ix_plants_plant_profile",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "IX_plants_plant_profile_id_farm_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "uq_plants_farm_plant_profile",
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

            migrationBuilder.DropUniqueConstraint(
                name: "uq_mapping_candidates_id_batch",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_mapping_candidate_batches_id_tenant_farm",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropIndex(
                name: "ix_mapping_candidate_batches_capture_attempt_mission",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropIndex(
                name: "uq_farm_base_map_versions_one_baseline",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropIndex(
                name: "uq_farm_base_map_versions_one_draft",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropIndex(
                name: "uq_farm_base_map_versions_one_published",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropIndex(
                name: "uq_farm_base_map_versions_tenant_farm_version",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_farm_base_map_versions_baseline_lineage",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_farm_base_map_versions_publication",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "verified_pole_count_snapshot_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "plant_profile_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "boundary_tolerance_m",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "capture_attempt_id",
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
                name: "payload_fingerprint",
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

            migrationBuilder.DropColumn(
                name: "internally_approved_map_version_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "map_internally_approved_at",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.AlterColumn<Guid>(
                name: "source_observation_id",
                schema: "farm",
                table: "mapping_candidates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_farm_version",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "farm_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_one_published",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "farm_id",
                unique: true,
                filter: "status = 'PUBLISHED'::system.farm_base_map_status");

            migrationBuilder.AddCheckConstraint(
                name: "ck_farm_base_map_versions_baseline_lineage",
                schema: "farm",
                table: "farm_base_map_versions",
                sql: "(is_baseline = true AND predecessor_version_id IS NULL) OR (is_baseline = false)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_farm_base_map_versions_publication",
                schema: "farm",
                table: "farm_base_map_versions",
                sql: "(status = 'DRAFT'::system.farm_base_map_status AND published_by IS NULL AND published_at IS NULL) OR (status IN ('PUBLISHED'::system.farm_base_map_status, 'SUPERSEDED'::system.farm_base_map_status) AND published_by IS NOT NULL AND published_at IS NOT NULL)");
        }
    }
}

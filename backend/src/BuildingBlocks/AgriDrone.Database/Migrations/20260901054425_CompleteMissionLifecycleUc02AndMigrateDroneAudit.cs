using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMissionLifecycleUc02AndMigrateDroneAudit
        : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Validate legacy data before the non-transactional PostgreSQL
            // enum change so a known data problem cannot leave a partial
            // migration behind.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM mission.drone_missions
                        WHERE zone_id IS NULL
                    )
                    THEN
                        RAISE EXCEPTION
                            'Legacy missions must be assigned a zone_id before UC02 can be migrated.';
                    END IF;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM mission.drone_missions AS mission
                        WHERE
                            mission.mission_type =
                                'HEALTH_INSPECTION'::system.mission_type
                            AND NOT EXISTS
                            (
                                SELECT 1
                                FROM farm.zone_map_versions AS map_version
                                WHERE
                                    map_version.farm_id = mission.farm_id
                                    AND map_version.zone_id = mission.zone_id
                                    AND map_version.status =
                                        'CONFIRMED'::system.map_version_status
                            )
                    )
                    THEN
                        RAISE EXCEPTION
                            'Every legacy health-inspection mission must have a confirmed map in its zone before UC02 can be migrated.';
                    END IF;
                END
                $$;
                """);

            // Chuyển lịch sử trạng thái Drone sang audit chung của BE1
            // trước khi xóa bảng audit riêng.
            migrationBuilder.Sql(
                """
                INSERT INTO system.audit_logs
                (
                    user_id,
                    tenant_id,
                    actor_type,
                    actor_id,
                    entity_type,
                    entity_id,
                    action,
                    old_data,
                    new_data,
                    created_at
                )
                SELECT
                    changed_by,
                    tenant_id,
                    'USER'::system.audit_actor_type,
                    changed_by,
                    'Drone',
                    drone_id,
                    CASE
                        WHEN previous_status IS NULL THEN 'REGISTER'
                        ELSE 'CHANGE_STATUS'
                    END,
                    CASE
                        WHEN previous_status IS NULL THEN NULL
                        ELSE jsonb_build_object(
                            'Status',
                            previous_status::text)
                    END,
                    jsonb_build_object(
                        'Status',
                        new_status::text),
                    changed_at
                FROM mission.drone_status_changes;
                """);

            migrationBuilder.DropTable(
                name: "drone_status_changes",
                schema: "mission");

            // Npgsql không tự động rename hoặc remove PostgreSQL enum label.
            // Các label cũ được rename để dữ liệu Mission hiện hữu
            // tự động chuyển sang tên trạng thái mới.
            migrationBuilder.Sql(
                """
                ALTER TYPE system.mission_status
                    RENAME VALUE 'READY' TO 'SCHEDULED';

                ALTER TYPE system.mission_status
                    RENAME VALUE 'FLYING' TO 'IN_FLIGHT';

                ALTER TYPE system.mission_status
                    RENAME VALUE 'FAILED' TO 'FLIGHT_FAILED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'FLIGHT_COMPLETED'
                    BEFORE 'COMPLETED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'UPLOADING'
                    BEFORE 'COMPLETED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'READY_FOR_PROCESSING'
                    BEFORE 'COMPLETED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'PROCESSING'
                    BEFORE 'COMPLETED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'AWAITING_REVIEW'
                    BEFORE 'COMPLETED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'UPLOAD_FAILED';

                ALTER TYPE system.mission_status
                    ADD VALUE IF NOT EXISTS 'PROCESSING_FAILED';
                """,
                suppressTransaction: true);

            // Enum values added above must be committed before PostgreSQL can
            // use them. Preserve the meaning of legacy COMPLETED rows instead
            // of treating every finished flight as a completed business flow.
            migrationBuilder.Sql(
                """
                UPDATE mission.drone_missions
                SET status =
                    CASE
                        WHEN processing_status =
                            'COMPLETED'::system.processing_status
                        THEN 'COMPLETED'::system.mission_status

                        WHEN processing_status =
                            'REVIEW_REQUIRED'::system.processing_status
                        THEN 'AWAITING_REVIEW'::system.mission_status

                        ELSE 'FLIGHT_COMPLETED'::system.mission_status
                    END
                WHERE status = 'COMPLETED'::system.mission_status;
                """);

            // Không tự động gán Guid.Empty vì zone_id có foreign key.
            // Migration sẽ dừng với thông báo rõ ràng nếu còn Mission
            // chưa được gắn Zone.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM mission.drone_missions
                        WHERE zone_id IS NULL
                    )
                    THEN
                        RAISE EXCEPTION
                            'Cannot make mission.drone_missions.zone_id required because legacy missions still have NULL zone_id.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "zone_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preflight_confirmed_at",
                schema: "mission",
                table: "drone_missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "preflight_confirmed_by",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_map_version_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            // Health missions created by the legacy model did not store the
            // source map. Backfill the current confirmed map of the same Zone.
            migrationBuilder.Sql(
                """
                UPDATE mission.drone_missions AS mission
                SET source_map_version_id = map_version.id
                FROM farm.zone_map_versions AS map_version
                WHERE
                    mission.mission_type =
                        'HEALTH_INSPECTION'::system.mission_type
                    AND mission.source_map_version_id IS NULL
                    AND map_version.farm_id = mission.farm_id
                    AND map_version.zone_id = mission.zone_id
                    AND map_version.status =
                        'CONFIRMED'::system.map_version_status;
                """);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "mission",
                table: "drone_missions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // HealthInspection cũ phải có source map trước khi bật constraint.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM mission.drone_missions
                        WHERE
                            mission_type =
                                'HEALTH_INSPECTION'::system.mission_type
                            AND source_map_version_id IS NULL
                    )
                    THEN
                        RAISE EXCEPTION
                            'Health-inspection missions must be assigned a source_map_version_id before this migration can continue.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_drone_missions_preflight_confirmation",
                schema: "mission",
                table: "drone_missions",
                sql:
                    "(preflight_confirmed_by IS NULL AND " +
                    "preflight_confirmed_at IS NULL) OR " +
                    "(preflight_confirmed_by IS NOT NULL AND " +
                    "preflight_confirmed_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drone_missions_source_map",
                schema: "mission",
                table: "drone_missions",
                sql:
                    "(mission_type = " +
                    "'MAPPING'::system.mission_type AND " +
                    "source_map_version_id IS NULL) OR " +
                    "(mission_type = " +
                    "'HEALTH_INSPECTION'::system.mission_type AND " +
                    "source_map_version_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_preflight_confirmation",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_source_map",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_confirmed_at",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_confirmed_by",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "source_map_version_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.AlterColumn<Guid>(
                name: "zone_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Đưa các trạng thái mới về trạng thái gần nhất mà model cũ
            // có thể hiểu trước khi rename enum label trở lại.
            migrationBuilder.Sql(
                """
                UPDATE mission.drone_missions
                SET status =
                    CASE
                        WHEN status::text IN
                        (
                            'FLIGHT_COMPLETED',
                            'UPLOADING',
                            'READY_FOR_PROCESSING',
                            'PROCESSING',
                            'AWAITING_REVIEW'
                        )
                        THEN 'SCHEDULED'::system.mission_status

                        WHEN status::text IN
                        (
                            'UPLOAD_FAILED',
                            'PROCESSING_FAILED'
                        )
                        THEN 'FLIGHT_FAILED'::system.mission_status

                        ELSE status
                    END;

                ALTER TYPE system.mission_status
                    RENAME VALUE 'SCHEDULED' TO 'READY';

                ALTER TYPE system.mission_status
                    RENAME VALUE 'IN_FLIGHT' TO 'FLYING';

                ALTER TYPE system.mission_status
                    RENAME VALUE 'FLIGHT_FAILED' TO 'FAILED';
                """);

            // PostgreSQL không hỗ trợ xóa trực tiếp những enum label mới.
            // Các label bổ sung sẽ còn trong enum nhưng không còn được
            // dữ liệu hoặc model cũ sử dụng.

            migrationBuilder.CreateTable(
                name: "drone_status_changes",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),
                    changed_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false),
                    changed_by = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),
                    drone_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),
                    new_status = table.Column<int>(
                        type: "system.drone_status",
                        nullable: false),
                    previous_status = table.Column<int>(
                        type: "system.drone_status",
                        nullable: true),
                    tenant_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_drone_status_changes",
                        x => x.id);

                    table.ForeignKey(
                        name: "fk_drone_status_changes_drone_same_tenant",
                        columns: x => new
                        {
                            x.drone_id,
                            x.tenant_id
                        },
                        principalSchema: "mission",
                        principalTable: "drones",
                        principalColumns: new[]
                        {
                            "id",
                            "tenant_id"
                        },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drone_status_changes_drone_changed_at",
                schema: "mission",
                table: "drone_status_changes",
                columns: new[]
                {
                    "tenant_id",
                    "drone_id",
                    "changed_at"
                });

            migrationBuilder.CreateIndex(
                name: "IX_drone_status_changes_drone_id_tenant_id",
                schema: "mission",
                table: "drone_status_changes",
                columns: new[]
                {
                    "drone_id",
                    "tenant_id"
                });
        }
    }
}

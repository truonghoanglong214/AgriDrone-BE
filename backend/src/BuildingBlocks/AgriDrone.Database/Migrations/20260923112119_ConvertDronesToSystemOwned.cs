using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConvertDronesToSystemOwned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    collisions text;
                BEGIN
                    SELECT string_agg(
                        format('%s=%s tenants=[%s]', key_type, key_value, tenant_ids),
                        '; ' ORDER BY key_type, key_value)
                    INTO collisions
                    FROM
                    (
                        SELECT
                            'code' AS key_type,
                            code AS key_value,
                            string_agg(tenant_id::text, ', ' ORDER BY tenant_id::text) AS tenant_ids
                        FROM mission.drones
                        GROUP BY code
                        HAVING count(*) > 1

                        UNION ALL

                        SELECT
                            'serial_number',
                            serial_number,
                            string_agg(tenant_id::text, ', ' ORDER BY tenant_id::text)
                        FROM mission.drones
                        WHERE serial_number IS NOT NULL
                        GROUP BY serial_number
                        HAVING count(*) > 1

                        UNION ALL

                        SELECT
                            'registration_number',
                            registration_number,
                            string_agg(tenant_id::text, ', ' ORDER BY tenant_id::text)
                        FROM mission.drones
                        WHERE registration_number IS NOT NULL
                        GROUP BY registration_number
                        HAVING count(*) > 1
                    ) duplicate_keys;

                    IF collisions IS NOT NULL THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23505',
                            MESSAGE = 'Cannot convert drones to system ownership because global identifiers collide: ' || collisions,
                            HINT = 'Resolve each physical-drone collision manually. Do not merge records automatically.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "drone_legacy_tenant_ownership",
                schema: "mission",
                columns: table => new
                {
                    drone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_drone_legacy_tenant_ownership",
                        x => x.drone_id);
                    table.ForeignKey(
                        name: "fk_drone_legacy_ownership_drones_drone_id",
                        column: x => x.drone_id,
                        principalSchema: "mission",
                        principalTable: "drones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_drone_legacy_ownership_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identity",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drone_legacy_tenant_ownership_tenant_id",
                schema: "mission",
                table: "drone_legacy_tenant_ownership",
                column: "tenant_id");

            migrationBuilder.Sql(
                """
                INSERT INTO mission.drone_legacy_tenant_ownership
                    (drone_id, tenant_id)
                SELECT id, tenant_id
                FROM mission.drones;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE mission.drone_missions
                DROP CONSTRAINT IF EXISTS
                    ex_drone_missions_no_schedule_overlap;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_drones_same_tenant",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropForeignKey(
                name: "fk_drones_tenants_tenant_id",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_drones_id_tenant",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "ix_drones_tenant",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_tenant_code",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_tenant_registration_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_tenant_serial_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "IX_drone_missions_drone_id_tenant_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "ix_drone_missions_drone_schedule",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "mission",
                table: "drones");

            migrationBuilder.AlterTable(
                name: "drones",
                schema: "mission",
                comment: "System-owned physical drone inventory managed centrally by AgriDrone.",
                oldComment: "Tenant-owned physical drone inventory reusable across farms in the same tenant.");

            migrationBuilder.CreateIndex(
                name: "uq_drones_code",
                schema: "mission",
                table: "drones",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_drones_registration_number",
                schema: "mission",
                table: "drones",
                column: "registration_number",
                unique: true,
                filter: "registration_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_drones_serial_number",
                schema: "mission",
                table: "drones",
                column: "serial_number",
                unique: true,
                filter: "serial_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_drone_schedule",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "drone_id", "scheduled_at", "scheduled_end_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_drones_drone_id",
                schema: "mission",
                table: "drone_missions",
                column: "drone_id",
                principalSchema: "mission",
                principalTable: "drones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE mission.drone_missions
                ADD CONSTRAINT ex_drone_missions_no_schedule_overlap
                EXCLUDE USING gist
                (
                    drone_id WITH =,
                    tstzrange(
                        scheduled_at,
                        scheduled_end_at,
                        '[)'
                    ) WITH &&
                )
                WHERE
                (
                    status IN
                    (
                        'SCHEDULED'::system.mission_status,
                        'IN_FLIGHT'::system.mission_status
                    )
                    AND scheduled_at IS NOT NULL
                    AND scheduled_end_at IS NOT NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE mission.drone_missions
                DROP CONSTRAINT IF EXISTS
                    ex_drone_missions_no_schedule_overlap;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_drones_drone_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "uq_drones_code",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_registration_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_serial_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "ix_drone_missions_drone_schedule",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.AlterTable(
                name: "drones",
                schema: "mission",
                comment: "Tenant-owned physical drone inventory reusable across farms in the same tenant.",
                oldComment: "System-owned physical drone inventory managed centrally by AgriDrone.");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "mission",
                table: "drones",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE mission.drones AS drone
                SET tenant_id = ownership.tenant_id
                FROM mission.drone_legacy_tenant_ownership AS ownership
                WHERE ownership.drone_id = drone.id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "mission",
                table: "drones",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_drones_id_tenant",
                schema: "mission",
                table: "drones",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_drones_tenant",
                schema: "mission",
                table: "drones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_drones_tenant_code",
                schema: "mission",
                table: "drones",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_drones_tenant_registration_number",
                schema: "mission",
                table: "drones",
                columns: new[] { "tenant_id", "registration_number" },
                unique: true,
                filter: "registration_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_drones_tenant_serial_number",
                schema: "mission",
                table: "drones",
                columns: new[] { "tenant_id", "serial_number" },
                unique: true,
                filter: "serial_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_drone_id_tenant_id",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "drone_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_drone_schedule",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "tenant_id", "drone_id", "scheduled_at", "scheduled_end_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_drones_same_tenant",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "drone_id", "tenant_id" },
                principalSchema: "mission",
                principalTable: "drones",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_drones_tenants_tenant_id",
                schema: "mission",
                table: "drones",
                column: "tenant_id",
                principalSchema: "identity",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE mission.drone_missions
                ADD CONSTRAINT ex_drone_missions_no_schedule_overlap
                EXCLUDE USING gist
                (
                    tenant_id WITH =,
                    drone_id WITH =,
                    tstzrange(
                        scheduled_at,
                        scheduled_end_at,
                        '[)'
                    ) WITH &&
                )
                WHERE
                (
                    status IN
                    (
                        'SCHEDULED'::system.mission_status,
                        'IN_FLIGHT'::system.mission_status
                    )
                    AND scheduled_at IS NOT NULL
                    AND scheduled_end_at IS NOT NULL
                );
                """);

            migrationBuilder.DropTable(
                name: "drone_legacy_tenant_ownership",
                schema: "mission");
        }
    }
}

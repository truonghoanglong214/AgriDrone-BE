using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDroneMaintenanceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "drone_maintenance_records",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    drone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_status = table.Column<int>(type: "system.drone_status", nullable: true),
                    next_maintenance_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drone_maintenance_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_drone_maintenance_records_drones_drone_id",
                        column: x => x.drone_id,
                        principalSchema: "mission",
                        principalTable: "drones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_drone_maintenance_history",
                schema: "mission",
                table: "drone_maintenance_records",
                columns: new[] { "drone_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "uq_drone_maintenance_open",
                schema: "mission",
                table: "drone_maintenance_records",
                column: "drone_id",
                unique: true,
                filter: "closed_at IS NULL");

            migrationBuilder.Sql(
                """
                INSERT INTO mission.drone_maintenance_records
                    (id, drone_id, started_at, reason)
                SELECT gen_random_uuid(), id, updated_at, 'LEGACY_MAINTENANCE'
                FROM mission.drones
                WHERE status = 'MAINTENANCE'::system.drone_status
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "drone_maintenance_records",
                schema: "mission");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMappingMessagingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_approval_id",
                schema: "farm",
                table: "zone_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "map_published_at",
                schema: "mission",
                table: "drone_missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "mapping_approval_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_map_version_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_zone_map_versions_source_approval",
                schema: "farm",
                table: "zone_map_versions",
                column: "source_approval_id",
                unique: true,
                filter: "source_approval_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_drone_missions_mapping_approval",
                schema: "mission",
                table: "drone_missions",
                column: "mapping_approval_id",
                unique: true,
                filter: "mapping_approval_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_zone_map_versions_source_approval",
                schema: "farm",
                table: "zone_map_versions");

            migrationBuilder.DropIndex(
                name: "ux_drone_missions_mapping_approval",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "source_approval_id",
                schema: "farm",
                table: "zone_map_versions");

            migrationBuilder.DropColumn(
                name: "map_published_at",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "mapping_approval_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "published_map_version_id",
                schema: "mission",
                table: "drone_missions");
        }
    }
}

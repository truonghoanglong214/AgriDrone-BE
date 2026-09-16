using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantConditionVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_plant_conditions_code",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retired_at",
                schema: "plant",
                table: "plant_conditions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "revision_number",
                schema: "plant",
                table: "plant_conditions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_id",
                schema: "plant",
                table: "plant_conditions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "plant",
                table: "plant_conditions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "uq_plant_conditions_active_code",
                schema: "plant",
                table: "plant_conditions",
                column: "code",
                unique: true,
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_plant_conditions_code_revision",
                schema: "plant",
                table: "plant_conditions",
                columns: new[] { "code", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_plant_conditions_supersedes",
                schema: "plant",
                table: "plant_conditions",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_plant_conditions_active_code",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropIndex(
                name: "uq_plant_conditions_code_revision",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropIndex(
                name: "uq_plant_conditions_supersedes",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "retired_at",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "revision_number",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "supersedes_id",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.CreateIndex(
                name: "uq_plant_conditions_code",
                schema: "plant",
                table: "plant_conditions",
                column: "code",
                unique: true);
        }
    }
}

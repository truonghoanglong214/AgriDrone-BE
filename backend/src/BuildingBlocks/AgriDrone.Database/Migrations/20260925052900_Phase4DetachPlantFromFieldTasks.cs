using AgriDrone.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20260925052900_Phase4DetachPlantFromFieldTasks")]
public sealed class Phase4DetachPlantFromFieldTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_plant_scans_source_task_same_farm",
            schema: "plant",
            table: "plant_scans");

        migrationBuilder.DropIndex(
            name: "IX_plant_scans_source_task_id_farm_id",
            schema: "plant",
            table: "plant_scans");

        migrationBuilder.DropColumn(
            name: "source_task_id",
            schema: "plant",
            table: "plant_scans");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "source_task_id",
            schema: "plant",
            table: "plant_scans",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_plant_scans_source_task_id_farm_id",
            schema: "plant",
            table: "plant_scans",
            columns: new[] { "source_task_id", "farm_id" });

        migrationBuilder.AddForeignKey(
            name: "fk_plant_scans_source_task_same_farm",
            schema: "plant",
            table: "plant_scans",
            columns: new[] { "source_task_id", "farm_id" },
            principalSchema: "field_task",
            principalTable: "field_tasks",
            principalColumns: new[] { "id", "farm_id" },
            onDelete: ReferentialAction.Restrict);
    }
}

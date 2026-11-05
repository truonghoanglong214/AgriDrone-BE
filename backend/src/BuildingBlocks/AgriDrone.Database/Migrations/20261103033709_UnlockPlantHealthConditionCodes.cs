using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class UnlockPlantHealthConditionCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_conditions_health_label_code",
                schema: "plant",
                table: "plant_conditions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_conditions_health_label_code",
                schema: "plant",
                table: "plant_conditions",
                sql: "plant_health_label_kind IS NULL OR (plant_health_label_kind = 'SuspectedDisease' AND code IN ('BROWN_SPOT', 'ANTHRACNOSE')) OR (plant_health_label_kind = 'ConfusingNonDisease' AND code IN ('SUNBURN', 'MECHANICAL_SCAR'))");
        }
    }
}

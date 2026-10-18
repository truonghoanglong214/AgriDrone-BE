using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step3OrderCompatibilityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "previous_order_compatibility_policy_version",
                schema: "survey",
                table: "survey_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "previous_order_compatibility_reason",
                schema: "survey",
                table: "survey_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_orders_previous_compatibility_decision",
                schema: "survey",
                table: "survey_orders",
                sql: "(previous_order_compatibility_policy_version IS NULL AND previous_order_compatibility_reason IS NULL) OR (length(btrim(previous_order_compatibility_policy_version)) > 0 AND length(btrim(previous_order_compatibility_reason)) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_orders_previous_compatibility_decision",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "previous_order_compatibility_policy_version",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "previous_order_compatibility_reason",
                schema: "survey",
                table: "survey_orders");
        }
    }
}

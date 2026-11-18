using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationV26PostFlightSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "booked_price_per_pole",
                schema: "survey",
                table: "survey_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "booked_service_price_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<SurveyServiceType>(
                name: "booked_service_type",
                schema: "survey",
                table: "survey_orders",
                type: "system.survey_service_type",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "post_flight_settlement",
                schema: "survey",
                table: "survey_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "service_reviewed_at",
                schema: "survey",
                table: "survey_orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "booked_price_per_pole",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_service_price_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "booked_service_type",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "post_flight_settlement",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "service_reviewed_at",
                schema: "survey",
                table: "survey_orders");
        }
    }
}

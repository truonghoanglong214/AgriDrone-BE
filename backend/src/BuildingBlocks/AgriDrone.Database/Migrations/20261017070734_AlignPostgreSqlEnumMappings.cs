using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AlignPostgreSqlEnumMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<SurveyAppointmentPurpose>(
                name: "purpose",
                schema: "survey",
                table: "survey_appointments",
                type: "system.survey_appointment_purpose",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "system.survey_appointment_purpose",
                oldDefaultValueSql: "'PAID_SERVICE'::system.survey_appointment_purpose");

            migrationBuilder.UpdateData(
                schema: "survey",
                table: "survey_services",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "service_type", "status" },
                values: new object[] { SurveyServiceType.PlantHealth, SurveyServiceStatus.Active });

            migrationBuilder.UpdateData(
                schema: "survey",
                table: "survey_services",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "service_type", "status" },
                values: new object[] { SurveyServiceType.HarvestReadiness, SurveyServiceStatus.Experimental });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "purpose",
                schema: "survey",
                table: "survey_appointments",
                type: "system.survey_appointment_purpose",
                nullable: false,
                defaultValueSql: "'PAID_SERVICE'::system.survey_appointment_purpose",
                oldClrType: typeof(SurveyAppointmentPurpose),
                oldType: "system.survey_appointment_purpose");

            migrationBuilder.UpdateData(
                schema: "survey",
                table: "survey_services",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "service_type", "status" },
                values: new object[] { 0, 1 });

            migrationBuilder.UpdateData(
                schema: "survey",
                table: "survey_services",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "service_type", "status" },
                values: new object[] { 1, 0 });
        }
    }
}

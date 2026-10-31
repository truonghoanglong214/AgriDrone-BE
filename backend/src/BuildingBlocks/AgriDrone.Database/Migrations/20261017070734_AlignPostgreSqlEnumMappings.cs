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
        }
    }
}

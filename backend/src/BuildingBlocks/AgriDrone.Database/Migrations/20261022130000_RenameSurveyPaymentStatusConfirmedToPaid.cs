using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

public sealed partial class RenameSurveyPaymentStatusConfirmedToPaid : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TYPE system.survey_payment_status RENAME VALUE 'CONFIRMED' TO 'PAID';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TYPE system.survey_payment_status RENAME VALUE 'PAID' TO 'CONFIRMED';");
    }
}

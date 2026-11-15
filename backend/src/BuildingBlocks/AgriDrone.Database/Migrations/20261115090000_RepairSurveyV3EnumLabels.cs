using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261115090000_RepairSurveyV3EnumLabels")]
public sealed class RepairSurveyV3EnumLabels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TYPE system.survey_appointment_purpose ADD VALUE IF NOT EXISTS 'CAPTURE';

            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_APPOINTMENT';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_DEPOSIT';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'READY_FOR_CAPTURE';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'CAPTURING';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_CAPTURE_UPLOAD';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_INPUT_REVIEW';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_PROCESSING';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'PROCESSING';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_REFLIGHT_DECISION';
            ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_SETTLEMENT';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous model already expects these labels, and existing rows may use them.
    }
}

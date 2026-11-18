using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261116090000_AllowManagerScheduledPlantHealthAppointments")]
public sealed class AllowManagerScheduledPlantHealthAppointments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "confirmed_by_system_manager_id",
            schema: "survey",
            table: "survey_appointments",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_survey_appointments_confirmed_by_system_manager_id",
            schema: "survey",
            table: "survey_appointments",
            column: "confirmed_by_system_manager_id");

        migrationBuilder.AddForeignKey(
            name: "fk_survey_appointments_users_confirmed_by_manager",
            schema: "survey",
            table: "survey_appointments",
            column: "confirmed_by_system_manager_id",
            principalSchema: "identity",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropCheckConstraint(
            name: "ck_survey_appointments_confirmation",
            schema: "survey",
            table: "survey_appointments");

        migrationBuilder.AddCheckConstraint(
            name: "ck_survey_appointments_confirmation",
            schema: "survey",
            table: "survey_appointments",
            sql: "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NOT NULL AND " +
                 "((confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_by_system_manager_id IS NULL) OR " +
                 "(purpose = 'PAID_SERVICE'::system.survey_appointment_purpose AND confirmed_by_tenant_owner_id IS NULL AND confirmed_by_system_manager_id IS NOT NULL))) OR " +
                 "(status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL AND confirmed_by_system_manager_id IS NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_survey_appointments_confirmation",
            schema: "survey",
            table: "survey_appointments");

        // Restoring the previous rule fails while a Manager-confirmed appointment still exists.
        migrationBuilder.AddCheckConstraint(
            name: "ck_survey_appointments_confirmation",
            schema: "survey",
            table: "survey_appointments",
            sql: "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_at IS NOT NULL) OR " +
                 "(status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL)");

        migrationBuilder.DropForeignKey(
            name: "fk_survey_appointments_users_confirmed_by_manager",
            schema: "survey",
            table: "survey_appointments");

        migrationBuilder.DropIndex(
            name: "IX_survey_appointments_confirmed_by_system_manager_id",
            schema: "survey",
            table: "survey_appointments");

        migrationBuilder.DropColumn(
            name: "confirmed_by_system_manager_id",
            schema: "survey",
            table: "survey_appointments");
    }
}

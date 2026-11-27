using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261127130000_V3FirstComboDiseaseCapture")]
public sealed class V3FirstComboDiseaseCapture : Migration
{
    private static string ConfirmationCheck(string managerPurpose, string ownerPurpose = "") =>
        "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NOT NULL AND " +
        $"(({ownerPurpose}confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_by_system_manager_id IS NULL) OR " +
        $"({managerPurpose} AND confirmed_by_tenant_owner_id IS NULL AND confirmed_by_system_manager_id IS NOT NULL))) OR " +
        "(status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL AND confirmed_by_system_manager_id IS NULL)";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TYPE system.survey_appointment_purpose ADD VALUE IF NOT EXISTS 'DISEASE_CAPTURE';");
        migrationBuilder.DropCheckConstraint("ck_survey_appointments_confirmation", "survey_appointments", "survey");
        migrationBuilder.AddCheckConstraint("ck_survey_appointments_confirmation", "survey_appointments",
            ConfirmationCheck("purpose::text IN ('PAID_SERVICE', 'DISEASE_CAPTURE')",
                "purpose::text <> 'DISEASE_CAPTURE' AND "), "survey");
        migrationBuilder.AddColumn<Guid>(name: "mapping_source_attempt_id", schema: "survey", table: "capture_attempts",
            type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(name: "uq_capture_attempts_disease_root", schema: "survey", table: "capture_attempts",
            column: "mapping_source_attempt_id", unique: true,
            filter: "mapping_source_attempt_id IS NOT NULL AND superseded_attempt_id IS NULL");
        migrationBuilder.CreateIndex(name: "ix_capture_attempts_mapping_source_owner", schema: "survey",
            table: "capture_attempts", columns: ["mapping_source_attempt_id", "tenant_id", "farm_id", "order_id"]);
        migrationBuilder.AddForeignKey(name: "fk_capture_attempts_mapping_source_owner", schema: "survey",
            table: "capture_attempts", columns: ["mapping_source_attempt_id", "tenant_id", "farm_id", "order_id"],
            principalSchema: "survey", principalTable: "capture_attempts",
            principalColumns: ["id", "tenant_id", "farm_id", "order_id"], onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM survey.survey_appointments WHERE purpose::text = 'DISEASE_CAPTURE') THEN
                    RAISE EXCEPTION 'Cannot roll back while disease capture appointments exist';
                END IF;
            END $$;
            """);
        migrationBuilder.DropForeignKey("fk_capture_attempts_mapping_source_owner", "capture_attempts", "survey");
        migrationBuilder.DropIndex("uq_capture_attempts_disease_root", "capture_attempts", "survey");
        migrationBuilder.DropIndex("ix_capture_attempts_mapping_source_owner", "capture_attempts", "survey");
        migrationBuilder.DropColumn("mapping_source_attempt_id", "capture_attempts", "survey");
        migrationBuilder.DropCheckConstraint("ck_survey_appointments_confirmation", "survey_appointments", "survey");
        migrationBuilder.AddCheckConstraint("ck_survey_appointments_confirmation", "survey_appointments",
            ConfirmationCheck("purpose = 'PAID_SERVICE'::system.survey_appointment_purpose"), "survey");
        // PostgreSQL does not support removing an enum value in place.
    }
}

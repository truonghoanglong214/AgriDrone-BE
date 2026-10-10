using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261010120000_AddUc03FlightSafetyEvidence")]
public sealed class AddUc03FlightSafetyEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "ck_mission_preflight_snapshot_object",
            schema: "mission", table: "mission_preflight_checklists");
        migrationBuilder.AddColumn<string>(name: "flight_authorization_evidence",
            schema: "mission", table: "mission_preflight_checklists", type: "text", nullable: true);
        migrationBuilder.AddCheckConstraint(name: "ck_mission_preflight_snapshot_object",
            schema: "mission", table: "mission_preflight_checklists",
            sql: "jsonb_typeof(definition_snapshot) = 'array'");

        migrationBuilder.AddColumn<string>(name: "incident_type", schema: "mission",
            table: "mission_field_notes", type: "character varying(32)", maxLength: 32,
            nullable: true);
        migrationBuilder.AddColumn<string>(name: "incident_outcome", schema: "mission",
            table: "mission_field_notes", type: "character varying(1000)", maxLength: 1000,
            nullable: true);
        migrationBuilder.AddColumn<string>(name: "recovery_decision", schema: "mission",
            table: "mission_field_notes", type: "character varying(32)", maxLength: 32,
            nullable: true);
        migrationBuilder.AddColumn<string>(name: "evidence_reference", schema: "mission",
            table: "mission_field_notes", type: "character varying(500)", maxLength: 500,
            nullable: true);
        migrationBuilder.AddCheckConstraint(name: "ck_mission_field_notes_incident", schema: "mission",
            table: "mission_field_notes", sql:
            "(incident_type IS NULL AND incident_outcome IS NULL AND " +
            "recovery_decision IS NULL AND evidence_reference IS NULL) OR " +
            "(incident_type IN ('SIGNAL_LOSS','LOW_BATTERY','INTERRUPTION','FLIGHT_FAILURE') " +
            "AND incident_outcome IS NOT NULL AND recovery_decision IS NOT NULL " +
            "AND evidence_reference IS NOT NULL AND length(trim(incident_outcome)) > 0 " +
            "AND recovery_decision IN ('CONTINUE','ABORT','RESCHEDULE_REQUIRED') " +
            "AND length(trim(evidence_reference)) > 0)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "ck_mission_field_notes_incident",
            schema: "mission", table: "mission_field_notes");
        migrationBuilder.DropColumn(name: "incident_type", schema: "mission", table: "mission_field_notes");
        migrationBuilder.DropColumn(name: "incident_outcome", schema: "mission", table: "mission_field_notes");
        migrationBuilder.DropColumn(name: "recovery_decision", schema: "mission", table: "mission_field_notes");
        migrationBuilder.DropColumn(name: "evidence_reference", schema: "mission", table: "mission_field_notes");
        migrationBuilder.DropColumn(name: "flight_authorization_evidence", schema: "mission",
            table: "mission_preflight_checklists");
        migrationBuilder.DropCheckConstraint(name: "ck_mission_preflight_snapshot_object",
            schema: "mission", table: "mission_preflight_checklists");
        migrationBuilder.AddCheckConstraint(name: "ck_mission_preflight_snapshot_object",
            schema: "mission", table: "mission_preflight_checklists",
            sql: "jsonb_typeof(definition_snapshot) = 'object'");
    }
}

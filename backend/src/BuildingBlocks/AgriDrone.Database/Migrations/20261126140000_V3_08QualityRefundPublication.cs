using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

public sealed partial class V3_08QualityRefundPublication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "survey_result_id", schema: "survey", table: "quality_refund_cases",
            type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
        migrationBuilder.AddColumn<Guid>(
            name: "v3_publication_id", schema: "survey", table: "quality_refund_cases",
            type: "uuid", nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "ck_quality_refund_result_source", schema: "survey", table: "quality_refund_cases",
            sql: "(survey_result_id IS NOT NULL) <> (v3_publication_id IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM survey.quality_refund_cases WHERE v3_publication_id IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot remove V3 quality refund publication links while V3 claims exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint(
            name: "ck_quality_refund_result_source", schema: "survey", table: "quality_refund_cases");
        migrationBuilder.DropColumn(name: "v3_publication_id", schema: "survey", table: "quality_refund_cases");
        migrationBuilder.AlterColumn<Guid>(
            name: "survey_result_id", schema: "survey", table: "quality_refund_cases",
            type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
    }
}

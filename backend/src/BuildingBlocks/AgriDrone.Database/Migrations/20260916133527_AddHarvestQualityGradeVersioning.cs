using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHarvestQualityGradeVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_quality_grades_code",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retired_at",
                schema: "harvest",
                table: "harvest_quality_grades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "revision_number",
                schema: "harvest",
                table: "harvest_quality_grades",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_id",
                schema: "harvest",
                table: "harvest_quality_grades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "harvest",
                table: "harvest_quality_grades",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_active_code",
                schema: "harvest",
                table: "harvest_quality_grades",
                column: "code",
                unique: true,
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_code_revision",
                schema: "harvest",
                table: "harvest_quality_grades",
                columns: new[] { "code", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_supersedes",
                schema: "harvest",
                table: "harvest_quality_grades",
                column: "supersedes_id",
                unique: true,
                filter: "supersedes_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_quality_grades_superseded_grade_id",
                schema: "harvest",
                table: "harvest_quality_grades",
                column: "supersedes_id",
                principalSchema: "harvest",
                principalTable: "harvest_quality_grades",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION harvest.prevent_quality_grade_semantic_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                       OR NEW.code IS DISTINCT FROM OLD.code
                       OR NEW.name IS DISTINCT FROM OLD.name
                       OR NEW.display_order IS DISTINCT FROM OLD.display_order
                       OR NEW.revision_number IS DISTINCT FROM OLD.revision_number
                       OR NEW.supersedes_id IS DISTINCT FROM OLD.supersedes_id
                       OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
                        RAISE EXCEPTION
                            'Harvest quality-grade revision semantics are immutable once created.'
                            USING ERRCODE = '23514';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_harvest_quality_grades_immutable_revision_semantics
                BEFORE UPDATE ON harvest.harvest_quality_grades
                FOR EACH ROW
                EXECUTE FUNCTION harvest.prevent_quality_grade_semantic_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_harvest_quality_grades_immutable_revision_semantics
                    ON harvest.harvest_quality_grades;

                DROP FUNCTION IF EXISTS harvest.prevent_quality_grade_semantic_change();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_quality_grades_superseded_grade_id",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropIndex(
                name: "uq_quality_grades_active_code",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropIndex(
                name: "uq_quality_grades_code_revision",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropIndex(
                name: "uq_quality_grades_supersedes",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropColumn(
                name: "retired_at",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropColumn(
                name: "revision_number",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropColumn(
                name: "supersedes_id",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_code",
                schema: "harvest",
                table: "harvest_quality_grades",
                column: "code",
                unique: true);
        }
    }
}

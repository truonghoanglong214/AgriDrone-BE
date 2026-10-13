using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class ProtectPlantConditionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_plant_conditions_superseded_condition_id",
                schema: "plant",
                table: "plant_conditions",
                column: "supersedes_id",
                principalSchema: "plant",
                principalTable: "plant_conditions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.prevent_plant_condition_semantic_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                       OR NEW.code IS DISTINCT FROM OLD.code
                       OR NEW.name IS DISTINCT FROM OLD.name
                       OR NEW.scientific_name IS DISTINCT FROM OLD.scientific_name
                       OR NEW.condition_type IS DISTINCT FROM OLD.condition_type
                       OR NEW.description IS DISTINCT FROM OLD.description
                       OR NEW.revision_number IS DISTINCT FROM OLD.revision_number
                       OR NEW.supersedes_id IS DISTINCT FROM OLD.supersedes_id
                       OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
                        RAISE EXCEPTION
                            'Plant condition revision semantics are immutable once created.'
                            USING ERRCODE = '23514';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_plant_conditions_immutable_revision_semantics
                BEFORE UPDATE ON plant.plant_conditions
                FOR EACH ROW
                EXECUTE FUNCTION plant.prevent_plant_condition_semantic_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_plant_conditions_immutable_revision_semantics
                    ON plant.plant_conditions;

                DROP FUNCTION IF EXISTS plant.prevent_plant_condition_semantic_change();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_plant_conditions_superseded_condition_id",
                schema: "plant",
                table: "plant_conditions");
        }
    }
}

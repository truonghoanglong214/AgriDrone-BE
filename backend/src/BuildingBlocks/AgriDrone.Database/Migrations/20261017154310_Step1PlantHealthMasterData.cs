using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step1PlantHealthMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "plant_condition_id",
                schema: "plant",
                table: "sourced_guidance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "plant_condition_revision_number",
                schema: "plant",
                table: "sourced_guidance",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retired_at",
                schema: "plant",
                table: "sourced_guidance",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "plant",
                table: "sourced_guidance",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE plant.sourced_guidance SET updated_at = created_at WHERE updated_at IS NULL;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "plant",
                table: "sourced_guidance",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "annotation_rule_version",
                schema: "plant",
                table: "plant_conditions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dataset_version",
                schema: "plant",
                table: "plant_conditions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plant_health_label_kind",
                schema: "plant",
                table: "plant_conditions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "team_label_limitation",
                schema: "plant",
                table: "plant_conditions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE plant.plant_conditions
                SET plant_health_label_kind = CASE
                        WHEN code IN ('BROWN_SPOT', 'ANTHRACNOSE') THEN 'SuspectedDisease'
                        WHEN code IN ('SUNBURN', 'MECHANICAL_SCAR') THEN 'ConfusingNonDisease'
                    END,
                    dataset_version = 'AGRIDRONE_PILOT_DATASET_V1',
                    annotation_rule_version = 'AGRIDRONE_ANNOTATION_RULES_V1',
                    team_label_limitation = 'Labels are produced by the AgriDrone team for pilot evaluation and are not expert-confirmed diagnoses.',
                    updated_at = NOW()
                WHERE code IN ('BROWN_SPOT', 'ANTHRACNOSE', 'SUNBURN', 'MECHANICAL_SCAR')
                  AND is_active = TRUE;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_plant_conditions_id_revision",
                schema: "plant",
                table: "plant_conditions",
                columns: new[] { "id", "revision_number" });

            migrationBuilder.CreateIndex(
                name: "ix_sourced_guidance_condition_revision",
                schema: "plant",
                table: "sourced_guidance",
                columns: new[] { "plant_condition_id", "plant_condition_revision_number" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourced_guidance_condition_version",
                schema: "plant",
                table: "sourced_guidance",
                sql: "(plant_condition_id IS NULL AND plant_condition_revision_number IS NULL) OR (plant_condition_id IS NOT NULL AND plant_condition_revision_number > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourced_guidance_effective_window",
                schema: "plant",
                table: "sourced_guidance",
                sql: "effective_to IS NULL OR effective_to > effective_from");

            migrationBuilder.CreateIndex(
                name: "ix_plant_conditions_health_label_active",
                schema: "plant",
                table: "plant_conditions",
                columns: new[] { "plant_health_label_kind", "is_active" },
                filter: "plant_health_label_kind IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_conditions_health_label_code",
                schema: "plant",
                table: "plant_conditions",
                sql: "plant_health_label_kind IS NULL OR (plant_health_label_kind = 'SuspectedDisease' AND code IN ('BROWN_SPOT', 'ANTHRACNOSE')) OR (plant_health_label_kind = 'ConfusingNonDisease' AND code IN ('SUNBURN', 'MECHANICAL_SCAR'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_conditions_health_label_kind",
                schema: "plant",
                table: "plant_conditions",
                sql: "plant_health_label_kind IS NULL OR plant_health_label_kind IN ('SuspectedDisease', 'ConfusingNonDisease')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_conditions_health_label_metadata",
                schema: "plant",
                table: "plant_conditions",
                sql: "(plant_health_label_kind IS NULL AND dataset_version IS NULL AND annotation_rule_version IS NULL AND team_label_limitation IS NULL) OR (plant_health_label_kind IS NOT NULL AND dataset_version IS NOT NULL AND annotation_rule_version IS NOT NULL AND team_label_limitation IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_conditions_health_label_type",
                schema: "plant",
                table: "plant_conditions",
                sql: "(plant_health_label_kind IS NULL) OR (plant_health_label_kind = 'SuspectedDisease' AND condition_type = 'DISEASE'::system.condition_type) OR (plant_health_label_kind = 'ConfusingNonDisease' AND condition_type <> 'DISEASE'::system.condition_type)");

            migrationBuilder.AddForeignKey(
                name: "fk_sourced_guidance_plant_condition",
                schema: "plant",
                table: "sourced_guidance",
                columns: new[] { "plant_condition_id", "plant_condition_revision_number" },
                principalSchema: "plant",
                principalTable: "plant_conditions",
                principalColumns: new[] { "id", "revision_number" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sourced_guidance_plant_condition",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropIndex(
                name: "ix_sourced_guidance_condition_revision",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourced_guidance_condition_version",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourced_guidance_effective_window",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_plant_conditions_id_revision",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropIndex(
                name: "ix_plant_conditions_health_label_active",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_conditions_health_label_code",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_conditions_health_label_kind",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_conditions_health_label_metadata",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_conditions_health_label_type",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "plant_condition_id",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropColumn(
                name: "plant_condition_revision_number",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropColumn(
                name: "retired_at",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "plant",
                table: "sourced_guidance");

            migrationBuilder.DropColumn(
                name: "annotation_rule_version",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "dataset_version",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "plant_health_label_kind",
                schema: "plant",
                table: "plant_conditions");

            migrationBuilder.DropColumn(
                name: "team_label_limitation",
                schema: "plant",
                table: "plant_conditions");
        }
    }
}

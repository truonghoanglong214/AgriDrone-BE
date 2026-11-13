using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class ValidateSurveyComboDiscountRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE survey.survey_combo_discount_policies IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.survey_combo_discount_policies
                               WHERE effective_to IS NULL AND discount_percentage >= 100) THEN
                        RAISE EXCEPTION 'The current Combo discount is 100 percent and cannot produce a payable per-pole price.'
                            USING HINT = 'Create a replacement policy below 100 percent using the Admin policy API before retrying. Historical policies and order prices are not modified.';
                    END IF;
                END;
                $migration$;
                """);
            migrationBuilder.Sql(
                "ALTER TABLE survey.survey_combo_discount_policies DROP CONSTRAINT IF EXISTS ck_survey_combo_discount_policies_percentage_valid;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_combo_discount_policies_percentage_valid",
                schema: "survey",
                table: "survey_combo_discount_policies",
                sql: "discount_percentage >= 0 AND discount_percentage <= 100 AND (effective_to IS NOT NULL OR discount_percentage < 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_combo_discount_policies_percentage_valid",
                schema: "survey",
                table: "survey_combo_discount_policies");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_combo_discount_policies_percentage_valid",
                schema: "survey",
                table: "survey_combo_discount_policies",
                sql: "discount_percentage >= 0 AND discount_percentage <= 100");
        }
    }
}

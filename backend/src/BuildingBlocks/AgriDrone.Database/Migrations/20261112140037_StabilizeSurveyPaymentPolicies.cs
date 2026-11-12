using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeSurveyPaymentPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Keep the check and index replacement atomic with policy writers.
                LOCK TABLE survey.survey_deposit_policies,
                    survey.survey_combo_discount_policies IN ACCESS EXCLUSIVE MODE;

                DO $migration$
                BEGIN
                    IF (SELECT count(*) FROM survey.survey_deposit_policies WHERE effective_to IS NULL) > 1
                        OR (SELECT count(*) FROM survey.survey_combo_discount_policies WHERE effective_to IS NULL) > 1 THEN
                        RAISE EXCEPTION 'Multiple current survey payment policies exist.'
                            USING HINT = 'Review the policy versions and close superseded policies before retrying. Existing order snapshots must not be repriced.';
                    END IF;
                END;
                $migration$;

                DROP INDEX IF EXISTS survey.uq_survey_deposit_policies_current;
                DROP INDEX IF EXISTS survey.uq_survey_combo_discount_policies_current;
                ALTER TABLE survey.survey_deposit_policies
                    DROP CONSTRAINT IF EXISTS ck_survey_deposit_policies_amount_positive;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_survey_deposit_policies_current",
                schema: "survey",
                table: "survey_deposit_policies",
                column: "effective_to",
                unique: true,
                filter: "effective_to IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_deposit_policies_amount_positive",
                schema: "survey",
                table: "survey_deposit_policies",
                sql: "amount_vnd >= 0 AND amount_vnd = trunc(amount_vnd)");

            migrationBuilder.CreateIndex(
                name: "uq_survey_combo_discount_policies_current",
                schema: "survey",
                table: "survey_combo_discount_policies",
                column: "effective_to",
                unique: true,
                filter: "effective_to IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE survey.survey_deposit_policies IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.survey_deposit_policies WHERE amount_vnd = 0) THEN
                        RAISE EXCEPTION 'Cannot restore the positive-only deposit constraint while zero-deposit policy history exists.'
                            USING HINT = 'Keep this migration applied to preserve zero-deposit policy history. No policy or order data has been deleted.';
                    END IF;
                END;
                $migration$;
                """);
            migrationBuilder.DropIndex(
                name: "uq_survey_deposit_policies_current",
                schema: "survey",
                table: "survey_deposit_policies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_deposit_policies_amount_positive",
                schema: "survey",
                table: "survey_deposit_policies");

            migrationBuilder.DropIndex(
                name: "uq_survey_combo_discount_policies_current",
                schema: "survey",
                table: "survey_combo_discount_policies");

            migrationBuilder.CreateIndex(
                name: "uq_survey_deposit_policies_current",
                schema: "survey",
                table: "survey_deposit_policies",
                column: "effective_to",
                unique: true,
                filter: "effective_to IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_deposit_policies_amount_positive",
                schema: "survey",
                table: "survey_deposit_policies",
                sql: "amount_vnd > 0 AND amount_vnd = trunc(amount_vnd)");

            migrationBuilder.CreateIndex(
                name: "uq_survey_combo_discount_policies_current",
                schema: "survey",
                table: "survey_combo_discount_policies",
                column: "effective_to",
                unique: true,
                filter: "effective_to IS NULL");
        }
    }
}

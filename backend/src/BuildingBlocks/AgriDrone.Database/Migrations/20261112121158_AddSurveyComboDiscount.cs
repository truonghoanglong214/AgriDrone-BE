using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyComboDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE survey.survey_orders
                    ADD COLUMN IF NOT EXISTS combo_discount_percentage numeric(5,2);

                ALTER TABLE plant.plant_change_events
                    ADD COLUMN IF NOT EXISTS farm_base_map_version_id uuid;

                CREATE TABLE IF NOT EXISTS survey.survey_combo_discount_policies (
                    id uuid NOT NULL,
                    version_number integer NOT NULL,
                    discount_percentage numeric(5,2) NOT NULL,
                    effective_from timestamp with time zone NOT NULL,
                    effective_to timestamp with time zone NULL,
                    created_by uuid NOT NULL,
                    CONSTRAINT pk_survey_combo_discount_policies PRIMARY KEY (id),
                    CONSTRAINT ck_survey_combo_discount_policies_percentage_valid CHECK (discount_percentage >= 0 AND discount_percentage <= 100)
                );

                CREATE TABLE IF NOT EXISTS survey.survey_deposit_policies (
                    id uuid NOT NULL,
                    version_number integer NOT NULL,
                    amount_vnd numeric(18,2) NOT NULL,
                    effective_from timestamp with time zone NOT NULL,
                    effective_to timestamp with time zone NULL,
                    created_by uuid NOT NULL,
                    CONSTRAINT pk_survey_deposit_policies PRIMARY KEY (id),
                    CONSTRAINT ck_survey_deposit_policies_amount_positive CHECK (amount_vnd > 0 AND amount_vnd = trunc(amount_vnd))
                );

                CREATE INDEX IF NOT EXISTS ix_plant_change_events_farm_base_map
                    ON plant.plant_change_events (farm_base_map_version_id);

                CREATE UNIQUE INDEX IF NOT EXISTS uq_survey_combo_discount_policies_current
                    ON survey.survey_combo_discount_policies (effective_to) WHERE effective_to IS NULL;

                CREATE UNIQUE INDEX IF NOT EXISTS uq_survey_combo_discount_policies_version
                    ON survey.survey_combo_discount_policies (version_number);

                CREATE UNIQUE INDEX IF NOT EXISTS uq_survey_deposit_policies_current
                    ON survey.survey_deposit_policies (effective_to) WHERE effective_to IS NULL;

                CREATE UNIQUE INDEX IF NOT EXISTS uq_survey_deposit_policies_version
                    ON survey.survey_deposit_policies (version_number);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_combo_discount_policies",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_deposit_policies",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "ix_plant_change_events_farm_base_map",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "combo_discount_percentage",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "farm_base_map_version_id",
                schema: "plant",
                table: "plant_change_events");
        }
    }
}

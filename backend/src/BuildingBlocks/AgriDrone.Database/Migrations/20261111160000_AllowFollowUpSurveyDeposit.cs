using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    [DbContext(typeof(AgriDroneSchemaDbContext))]
    [Migration("20261111160000_AllowFollowUpSurveyDeposit")]
    public partial class AllowFollowUpSurveyDeposit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE survey.survey_orders DROP CONSTRAINT IF EXISTS ck_survey_orders_deposit_consistency;

                ALTER TABLE survey.survey_orders
                    ADD CONSTRAINT ck_survey_orders_deposit_consistency
                    CHECK (
                        (deposit_amount = 0 AND deposit_payment_status = 'PAID'::system.survey_payment_status) OR
                        (deposit_amount > 0)
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE survey.survey_orders DROP CONSTRAINT IF EXISTS ck_survey_orders_deposit_consistency;

                ALTER TABLE survey.survey_orders
                    ADD CONSTRAINT ck_survey_orders_deposit_consistency
                    CHECK (
                        (requires_baseline_mapping = false AND deposit_amount = 0 AND deposit_payment_status = 'PAID'::system.survey_payment_status) OR
                        (requires_baseline_mapping = true AND deposit_amount > 0)
                    );
                """);
        }
    }
}


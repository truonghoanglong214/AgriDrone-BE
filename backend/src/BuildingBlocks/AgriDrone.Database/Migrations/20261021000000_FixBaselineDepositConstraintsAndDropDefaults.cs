using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

public sealed partial class FixBaselineDepositConstraintsAndDropDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE survey.survey_orders
            SET deposit_amount = 0,
                deposit_payment_status = 'CONFIRMED'::system.survey_payment_status
            WHERE requires_baseline_mapping = false;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE survey.survey_orders ALTER COLUMN deposit_amount DROP DEFAULT;
            ALTER TABLE survey.survey_orders ALTER COLUMN deposit_payment_status DROP DEFAULT;
            ALTER TABLE survey.survey_payments ALTER COLUMN purpose DROP DEFAULT;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE survey.survey_orders
                ADD CONSTRAINT ck_survey_orders_deposit_nonnegative
                CHECK (deposit_amount >= 0);

            ALTER TABLE survey.survey_orders
                ADD CONSTRAINT ck_survey_orders_remaining_amount_nonnegative
                CHECK (remaining_amount IS NULL OR remaining_amount >= 0);

            ALTER TABLE survey.survey_orders
                ADD CONSTRAINT ck_survey_orders_deposit_consistency
                CHECK (
                    (requires_baseline_mapping = false AND deposit_amount = 0 AND deposit_payment_status = 'CONFIRMED'::system.survey_payment_status) OR
                    (requires_baseline_mapping = true AND deposit_amount > 0)
                );

            ALTER TABLE survey.survey_payments
                ADD CONSTRAINT ck_survey_payments_purpose
                CHECK (purpose IN ('BaselineDeposit', 'FinalBalance'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE survey.survey_orders DROP CONSTRAINT IF EXISTS ck_survey_orders_deposit_consistency;
            ALTER TABLE survey.survey_orders DROP CONSTRAINT IF EXISTS ck_survey_orders_remaining_amount_nonnegative;
            ALTER TABLE survey.survey_orders DROP CONSTRAINT IF EXISTS ck_survey_orders_deposit_nonnegative;
            ALTER TABLE survey.survey_payments DROP CONSTRAINT IF EXISTS ck_survey_payments_purpose;

            ALTER TABLE survey.survey_orders ALTER COLUMN deposit_amount SET DEFAULT 500000;
            ALTER TABLE survey.survey_orders ALTER COLUMN deposit_payment_status SET DEFAULT 'PENDING'::system.survey_payment_status;
            ALTER TABLE survey.survey_payments ALTER COLUMN purpose SET DEFAULT 'FinalBalance';
            """);
    }
}

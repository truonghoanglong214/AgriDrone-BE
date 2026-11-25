using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261125124500_V3_06BalanceCheckoutOperations")]
public sealed class V3_06BalanceCheckoutOperations : Migration
{
    private const string Constraint = "ck_survey_result_review_operations_action";
    private const string Table = "survey_result_review_operations";
    private const string Schema = "survey";
    private const string PreviousActions =
        "'RecordComboReview', 'FreezeInternalResult', 'FinalizeInvoice', 'CorrectPreReleaseInvoice'";
    private const string CheckoutActions =
        "'BeginBalanceCheckout', 'CompleteBalanceCheckout', 'BalanceCheckoutUncertain'";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(Constraint, Table, Schema);
        migrationBuilder.AddCheckConstraint(
            Constraint, Table, $"action IN ({PreviousActions}, {CheckoutActions})", Schema);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(Constraint, Table, Schema);
        migrationBuilder.AddCheckConstraint(Constraint, Table, $"action IN ({PreviousActions})", Schema);
    }
}

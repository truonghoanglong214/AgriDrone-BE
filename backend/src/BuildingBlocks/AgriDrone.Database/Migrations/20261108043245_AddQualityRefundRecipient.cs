using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityRefundRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recipient_account_name",
                schema: "survey",
                table: "quality_refund_cases",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient_account_number",
                schema: "survey",
                table: "quality_refund_cases",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient_bank_name",
                schema: "survey",
                table: "quality_refund_cases",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recipient_account_name",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.DropColumn(
                name: "recipient_account_number",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.DropColumn(
                name: "recipient_bank_name",
                schema: "survey",
                table: "quality_refund_cases");
        }
    }
}

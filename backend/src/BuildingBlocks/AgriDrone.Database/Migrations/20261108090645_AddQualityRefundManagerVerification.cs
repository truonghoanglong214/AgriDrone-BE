using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityRefundManagerVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "manager_findings",
                schema: "survey",
                table: "quality_refund_cases",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manager_verification_reason",
                schema: "survey",
                table: "quality_refund_cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "manager_verified_at",
                schema: "survey",
                table: "quality_refund_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "manager_verified_by",
                schema: "survey",
                table: "quality_refund_cases",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "manager_findings",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.DropColumn(
                name: "manager_verification_reason",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.DropColumn(
                name: "manager_verified_at",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.DropColumn(
                name: "manager_verified_by",
                schema: "survey",
                table: "quality_refund_cases");
        }
    }
}

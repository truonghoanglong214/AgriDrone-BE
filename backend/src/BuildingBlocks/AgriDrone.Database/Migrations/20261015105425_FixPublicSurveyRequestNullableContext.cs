using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixPublicSurveyRequestNullableContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_boundaries_requests_same_tenant_farm",
                schema: "farm",
                table: "farm_boundaries");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_survey_requests_id_tenant_farm",
                schema: "survey",
                table: "survey_requests");

            migrationBuilder.DropIndex(
                name: "IX_farm_boundaries_source_survey_request_id_tenant_id_farm_id",
                schema: "farm",
                table: "farm_boundaries");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "survey",
                table: "survey_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "farm_id",
                schema: "survey",
                table: "survey_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_source_survey_request_id",
                schema: "farm",
                table: "farm_boundaries",
                column: "source_survey_request_id");

            migrationBuilder.AddForeignKey(
                name: "fk_farm_boundaries_requests_source_request_id",
                schema: "farm",
                table: "farm_boundaries",
                column: "source_survey_request_id",
                principalSchema: "survey",
                principalTable: "survey_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_boundaries_requests_source_request_id",
                schema: "farm",
                table: "farm_boundaries");

            migrationBuilder.DropIndex(
                name: "IX_farm_boundaries_source_survey_request_id",
                schema: "farm",
                table: "farm_boundaries");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "survey",
                table: "survey_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "farm_id",
                schema: "survey",
                table: "survey_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_survey_requests_id_tenant_farm",
                schema: "survey",
                table: "survey_requests",
                columns: new[] { "id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_source_survey_request_id_tenant_id_farm_id",
                schema: "farm",
                table: "farm_boundaries",
                columns: new[] { "source_survey_request_id", "tenant_id", "farm_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_farm_boundaries_requests_same_tenant_farm",
                schema: "farm",
                table: "farm_boundaries",
                columns: new[] { "source_survey_request_id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_requests",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}

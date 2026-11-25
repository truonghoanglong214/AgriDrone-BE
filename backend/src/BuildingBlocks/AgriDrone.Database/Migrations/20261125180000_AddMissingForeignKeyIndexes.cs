using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261125180000_AddMissingForeignKeyIndexes")]
public sealed class AddMissingForeignKeyIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_survey_combo_import_batches_order",
            schema: "survey",
            table: "survey_combo_import_batches",
            columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

        migrationBuilder.CreateIndex(
            name: "ix_refresh_session_tokens_predecessor_token",
            schema: "identity",
            table: "refresh_session_tokens",
            columns: new[] { "predecessor_token_id", "session_id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_refresh_session_tokens_predecessor_token",
            schema: "identity",
            table: "refresh_session_tokens");

        migrationBuilder.DropIndex(
            name: "ix_survey_combo_import_batches_order",
            schema: "survey",
            table: "survey_combo_import_batches");
    }
}

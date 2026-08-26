using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInitializationLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "initialization_locks",
                schema: "identity",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_initialization_locks", x => x.name);
                },
                comment: "Singleton rows used to serialize distributed initialization operations.");

            migrationBuilder.InsertData(
                schema: "identity",
                table: "initialization_locks",
                columns: new[] { "name", "version" },
                values: new object[] { "system-admin-bootstrap", 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "initialization_locks",
                schema: "identity");
        }
    }
}
